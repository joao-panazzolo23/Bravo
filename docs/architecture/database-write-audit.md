# Auditoria de Escritas no Banco de Dados — Bravo

> Escopo: todos os repositórios de persistência da solução (`Bravo.Infrastructure.PostgreSQL.Dapper`,
> `Bravo.Infrastructure.PostgreSQL`), com foco nos pipelines de importação IBGE e TSE.
>
> Data da análise: 2026-11-22. Base: `dac8ea1` mais as alterações de trabalho presentes no
> repositório no momento da auditoria.
>
> Este documento **não contém estimativas de ganho de performance inventadas**. Onde não há medição,
> o impacto é descrito como hipótese, com o mecanismo de banco que a sustenta.

---

## 1. Como a auditoria foi feita

Foram inspecionados todos os comandos que podem efetivamente escrever:

- `INSERT ... ON CONFLICT DO UPDATE` e `DO NOTHING`
- `UPDATE` avulsos
- `COPY ... FROM STDIN (FORMAT BINARY)` para tabelas temporárias de staging
- escritas via Entity Framework Core (`SaveChangesAsync`)

Não existe `MERGE` em nenhum ponto da solução. Não existem triggers, nem colunas de auditoria
alimentadas por trigger, em nenhuma migration
(`grep -riE "trigger|audit" src/Infrastructure/Bravo.Infrastructure.PostgreSQL --include=*.cs`
retorna apenas as migrations históricas). Portanto, o único efeito colateral observável de um
`UPDATE` neste esquema é:

1. nova versão física de linha (`xmin`), com WAL e dead tuple;
2. manutenção de índices;
3. alteração de `"UpdatedAt"`.

Esse fato é o que permite classificar com segurança um `UPDATE` como desnecessário quando todas as
colunas comparadas já são iguais. A verificação foi feita com `xmin` nos testes de integração
(`RowVersionAsync`, em `tests/Bravo.IntegrationTests/Importing/BulkPersistenceTestBase.cs`), que é
prova física de que a linha não foi reescrita.

### Convenção de classificação

| Classificação | Significado |
|---|---|
| **Confirmada** | A escrita não produz mudança de estado observável e pode ser eliminada sem alterar semântica. |
| **Potencial** | A escrita provavelmente é desnecessária, mas depende de semântica que exige decisão (ex.: `UpdatedAt` como sinal de vida). |
| **Legítima** | A escrita altera estado real ou tem efeito colateral intencional; deve permanecer. |

### Estado geral

O grosso das escritas em massa **já está protegido por guarda de no-op**. O padrão dominante nos
repositórios migrados para `INSERT ... SELECT` sobre staging com `COPY` binário é:

```sql
ON CONFLICT (<chave>) DO UPDATE SET ...
WHERE ROW(target.<colunas mutáveis>) IS DISTINCT FROM ROW(EXCLUDED.<colunas mutáveis>);
```

Isso significa que a maior parte das escritas repetidas de referência (estados, municípios,
partidos, federações, cargos, coalizões, locais de votação, seções, resultados) **já não reescreve
linhas idênticas**. A auditoria encontrou um conjunto pequeno de exceções reais e alguns casos de
atenção. Elas estão listadas abaixo, com evidência de código.

---

## 2. Resumo executivo

| # | Repositório | Método | Tabela | Classificação | Status |
|---|---|---|---|---|---|
| 1 | `CandidateRepository` | `UpsertIdentitiesAsync` (agora `EnsureIdentitiesAsync`) | `CandidateIdentities` | **Confirmada** | **Implementado** |
| 2 | `ElectionRepository` | `UpsertAsync`, `UpsertRoundAsync` | `Elections`, `ElectionRounds` | **Confirmada** | Recomendado, aguardando aprovação |
| 3 | `ImportDatasetRepository` | `RegisterAsync` | `ImportDatasets` | **Confirmada** | Recomendado, aguardando aprovação |
| 4 | `ElectoralStructureRepository` | `UpsertSectionsAsync` | `ElectoralSections` | **Potencial** | Recomendado, aguardando aprovação |
| 5 | `ImportJobRepository` | `ReportProgressAsync` / `RenewLeaseAsync` | `ImportJobs` | **Potencial** | Recomendado, aguardando aprovação |
| 6 | `DatasetVersionRepository` | `SetStatusAsync` (chamado em `RegisterAsync`) | `DatasetVersions` | **Potencial** | Recomendado, aguardando aprovação |
| 7 | `ImportFailedRecordRepository` | `UpsertAsync` | `ImportFailedRecords` | **Legítima** | Manter |
| 8 | `CandidateRepository` | `UpsertCandidatesAsync` | `Candidates` | **Legítima** | Manter |
| 9 | `ElectionResultRepository` | `UpsertResultsAsync` | `ElectionResults` | **Legítima** | Manter |
| 10 | `GeographyLookup` | `UpsertStatesAsync` / `UpsertMunicipalitiesAsync` | `States`, `Municipalities` | **Legítima** | Manter |
| 11 | `ElectoralReferenceRepository` | `UpsertOfficesAsync` / `UpsertPartiesAsync` / `UpsertFederationsAsync` | `ElectoralOffices`, `PoliticalParties`, `Federations` | **Legítima** | Manter |
| 12 | `CoalitionRepository`, `TseMunicipalityCodeRepository` | `UpsertAsync` | `Coalitions`, `TseMunicipalityCodes` | **Legítima** | Manter |

Total: **1 escrita confirmadamente desnecessária corrigida**, **2 confirmadas pendentes**,
**3 potenciais pendentes** e **6 casos legítimos** documentados para evitar que sejam “otimizados”
por engano no futuro.

---

## 3. Problemas confirmados

### 3.1 `CandidateIdentities` — UPSERT que reescrevia identidade imutável

| Campo | Valor |
|---|---|
| **Repositório** | `Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Electoral.CandidateRepository` |
| **Método** | `UpsertIdentitiesAsync` → renomeado para `EnsureIdentitiesAsync` |
| **Destino** | `CandidateIdentities` |
| **Comportamento anterior** | `INSERT ... ON CONFLICT ("Cpf") DO UPDATE SET "FullName"=EXCLUDED."FullName", "BirthDate"=EXCLUDED."BirthDate", "UpdatedAt"=EXCLUDED."UpdatedAt" RETURNING ...` (e o equivalente para a chave parcial `("FullName","BirthDate") WHERE "Cpf" IS NULL`). |
| **Evidência (antes)** | `CandidateRepository.cs`, constantes `MergeIdentitiesWithCpfSql` e `MergeIdentitiesWithoutCpfSql`, com o comentário explícito: *“Must update even when nothing changes: ON CONFLICT ... DO NOTHING would suppress the RETURNING row for an existing CPF, and the caller needs that id back.”* |
| **Problema** | `CandidateIdentity` é identidade permanente (`src/Domain/Bravo.Domain/Electoral/Entities/CandidateIdentity.cs`). Toda reimportação de qualquer ano/estado reescrevia a linha de identidade de cada candidato já existente. Pior: sobrescrevia atributos canônicos com os de uma fonte posterior, o que é uma mudança de dados, não um no-op. |
| **Impacto** | A cada execução do import de candidatos, uma versão física por identidade existente: WAL, dead tuples, manutenção dos índices `IX_CandidateIdentities_Cpf` e `IX_CandidateIdentities_FullName_BirthDate`, contenção de lock nas linhas compartilhadas por estados concorrentes. Além disso, a identidade deixava de ser imutável na prática. |
| **Correção proposta** | `INSERT ... ON CONFLICT DO NOTHING` nas duas partições, seguido de um único `SELECT` de leitura em massa que devolve `Id`, `Cpf`, `FullName`, `BirthDate` de todas as chaves do lote. |
| **Risco** | Nenhuma perda de ID: a leitura é feita por chave natural, exatamente a mesma que os índices únicos impõem. O risco real é semântico e foi tratado explicitamente: uma identidade existente com nome/data diferentes **não é mais sobrescrita**; a divergência é registrada como diagnóstico. |
| **Status** | **Implementado** (ver §4) |

### 3.2 `Elections` e `ElectionRounds` — UPSERT sem guarda

| Campo | Valor |
|---|---|
| **Repositório** | `Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Electoral.ElectionRepository` |
| **Métodos** | `UpsertAsync`, `UpsertRoundAsync` |
| **Destino** | `Elections`, `ElectionRounds` |
| **Comportamento atual** | `INSERT ... ON CONFLICT ("TseElectionCode") DO UPDATE SET "Year"=..., "Description"=..., "UpdatedAt"=EXCLUDED."UpdatedAt" RETURNING *` e o equivalente em `("ElectionId","Round")`. |
| **Evidência** | `ElectionRepository.cs:11-38`. Não há cláusula `WHERE`. |
| **Problema** | `UpsertElectionAsync` é chamado a cada ano de candidatos (`TseImportSource.References.cs:121-134`), ou seja, 17 vezes por job, para os mesmos poucos códigos de eleição. Toda chamada reescreve a linha, mesmo com `Year`, `TypeCode`, `TypeName`, `Description` e `ElectionDate` idênticos. |
| **Impacto** | Baixo em volume absoluto (algumas centenas de linhas por job), mas é reescrita garantida e desnecessária em toda execução. |
| **Correção proposta** | **Atenção: a correção ingênua está errada.** Verificado em PostgreSQL 16 que acrescentar `WHERE target."Name" IS DISTINCT FROM EXCLUDED."Name"` a um `DO UPDATE ... RETURNING *` faz o `RETURNING` **não devolver linha** quando nada muda — exatamente o problema que motivou o `DO UPDATE` em `CandidateIdentities`. Portanto a correção correta é o mesmo padrão já implementado aqui: `ON CONFLICT ("TseElectionCode") DO NOTHING` seguido de `SELECT * FROM "Elections" WHERE "TseElectionCode" = @code` (duas instruções, uma leitura indexada). Alternativa: manter o `DO UPDATE` com guarda e fazer o `SELECT` apenas quando `RETURNING` vier vazio, o que evita a leitura no caso comum mas mantém um caminho condicional. |
| **Risco** | O custo passa de 1 para 2 comandos por eleição (≈ centenas por job), em troca de zero `UPDATE`. O saldo só compensa se as eleições já existirem — o que é o caso a partir da segunda execução, e é o cenário que importa. |
| **Status** | **Recomendado — aguardando aprovação** |

### 3.3 `ImportDatasets` — UPSERT que só atualiza `UpdatedAt`

| Campo | Valor |
|---|---|
| **Repositório** | `Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Importing.ImportDatasetRepository` |
| **Método** | `RegisterAsync` |
| **Destino** | `ImportDatasets` |
| **Comportamento atual** | `INSERT ... ON CONFLICT ("Provider","Dataset","ElectionYear","Checksum") DO UPDATE SET "UpdatedAt" = EXCLUDED."UpdatedAt" RETURNING *`. |
| **Evidência** | `ImportDatasetRepository.cs:11-21`. |
| **Problema** | A chave de conflito inclui o `Checksum` do arquivo. Um conflito significa, por definição, **o mesmo arquivo já registrado**. A única coluna atualizada é `UpdatedAt`, ou seja, a escrita existe *apenas* para registrar “vi este arquivo de novo”. Nenhum dado de negócio muda. |
| **Impacto** | Uma reescrita por arquivo adquirido. No TSE isso é amplificado: `DatasetVersionService.RegisterAsync` é chamado por `(ano, estado)` em `TseImportSource.Results.cs:92`, e cada escopo re-registra o dataset. O volume é modesto (uma linha por escopo), mas o padrão é o mesmo que já foi corrigido nos outros repositórios. |
| **Correção proposta** | `DO UPDATE SET "UpdatedAt" = EXCLUDED."UpdatedAt" WHERE target."UpdatedAt" IS DISTINCT FROM EXCLUDED."UpdatedAt"` **não** resolve, porque `EXCLUDED."UpdatedAt"` é sempre novo. A correção correta é separar as responsabilidades: registrar o dataset com `ON CONFLICT DO NOTHING` e obter o `Id` com um `SELECT` pela chave natural, mantendo `AcquiredAt` como registro da primeira aquisição. Se for necessário saber “quando foi visto por último”, isso deve ser um campo explícito (`LastSeenAt`) e não `UpdatedAt`. |
| **Risco** | Quem consultar `UpdatedAt` para saber “última vez que o arquivo foi visto” perderá essa informação. Por isso a correção recomendada inclui introduzir `LastSeenAt` ou aceitar explicitamente a mudança de semântica. **Este é o item que mais exige decisão de produto antes de implementar.** |
| **Status** | **Recomendado — aguardando aprovação** |

---

## 4. Correção implementada — `CandidateIdentities`

### 4.1 Regras de unicidade confirmadas no banco

A tabela é criada em `Migrations/20261120030230_FirstMigration.cs:15-28` e tem dois índices únicos,
o segundo reforçado em `Migrations/20261120225129_FixUniqueIndexNullSemantics.cs:75-81`:

```sql
CREATE UNIQUE INDEX "IX_CandidateIdentities_Cpf"
    ON "CandidateIdentities" ("Cpf");

CREATE UNIQUE INDEX "IX_CandidateIdentities_FullName_BirthDate"
    ON "CandidateIdentities" ("FullName", "BirthDate") NULLS NOT DISTINCT
    WHERE "Cpf" IS NULL;
```

Configuração equivalente em `Mappings/ElectoralMaps/CandidateIdentityMap.cs:17-26`.

Conclusões:

- **A chave de CPF é exatamente o CPF normalizado.** `CandidateIdentity.Create` normaliza para
  exatamente 11 dígitos e devolve `null` caso contrário
  (`src/Domain/Bravo.Domain/Electoral/Entities/CandidateIdentity.cs:39-47`).
  `CandidateIdentityKey.NormaliseCpf` duplica essa regra
  (`src/Domain/Bravo.Domain/Electoral/Services/CandidateIdentityKey.cs:32-40`) e há teste garantindo
  que as duas concordam (`tests/Bravo.UnitTests/Electoral/CandidateIdentityKeyTests.cs:76-101`).
- **`NULLS NOT DISTINCT` é indispensável.** Existem candidatos do TSE sem CPF **e** sem data de
  nascimento. Sem ele, o índice parcial aceitaria duplicatas ilimitadas para `(nome, NULL)`.
- **CPF inválido não vira linha de CPF.** Sentinelas como `-4` e `#NULO` são descartadas e a linha
  passa a ser identificada por `(FullName, BirthDate)`. Isso é coberto por
  `ScopedElectoralStructureTests.InvalidCpfIsStoredAndKeyedAsAnIdentityWithoutCpf`.

Ou seja, a regra de unicidade do banco **corresponde** à semântica de identidade do domínio. Não foi
necessária nenhuma alteração de esquema.

### 4.2 O que foi alterado

**Domínio**

- Novo `src/Domain/Bravo.Domain/Electoral/Models/CandidateIdentityResolution.cs`:
  - `CandidateIdentityResolution(IdsByKey, Conflicts)`;
  - `CandidateIdentityResolution.KeyOf(identity)`, que usa `CandidateIdentityKey`;
  - `CandidateIdentityConflict(CandidateIdentityId, FullNameDiffers, BirthDateDiffers)` — **não
    possui nenhum campo de CPF**, portanto o diagnóstico nunca expõe dado sensível.
- `ICandidateRepository.UpsertIdentitiesAsync` foi substituído por
  `EnsureIdentitiesAsync`, que devolve `CandidateIdentityResolution`.

**Infraestrutura** (`CandidateRepository.cs`)

- `InsertIdentitiesWithCpfSql`: `INSERT ... ON CONFLICT ("Cpf") DO NOTHING`.
- `InsertIdentitiesWithoutCpfSql`: `INSERT ... ON CONFLICT ("FullName","BirthDate") WHERE "Cpf" IS NULL DO NOTHING`.
- `SelectPersistedIdentitiesSql`: um único `SELECT` com `UNION ALL` que resolve CPF e
  `(nome, data)` em uma só ida ao banco, usando `INNER JOIN` sobre a própria staging table. O join
  por `"BirthDate" IS NOT DISTINCT FROM` é obrigatório por causa das linhas sem data.
- O lote inteiro continua em **uma transação** criada por `BulkCopy.ExecuteAsync`
  (`Bulk/BulkCopy.cs:119-153`): `CREATE TEMP TABLE ... ON COMMIT DROP`, `COPY` binário, dois
  `INSERT` e um `SELECT`.

### 4.3 Concorrência

O ponto delicado é: o `INSERT ... DO NOTHING` não devolve a linha que perdeu o conflito. A solução é
a leitura posterior, e ela funciona porque:

1. O `INSERT ... ON CONFLICT DO NOTHING` **espera** a transação concorrente que segura o índice
   único; quando ela commita, o `INSERT` é pulado.
2. A leitura seguinte roda como **outro comando** na mesma transação, sob `READ COMMITTED` (default
   do servidor, confirmado com `SHOW default_transaction_isolation`). Em `READ COMMITTED`, cada
   comando pega um snapshot novo, então ele **enxerga** a linha recém-commitada pela concorrente.
3. Não é preciso `REPEATABLE READ` nem retry manual: a unicidade é arbitrada pelo índice e o ID é
   sempre recuperável.

O `BulkCopy` ainda envolve tudo em retry limitado para `deadlock_detected` e
`serialization_failure` (`Bulk/BulkCopy.cs:155-157`), o que cobre o caso de deadlock entre escopos
concorrentes.

A ordenação `ORDER BY "Cpf"` / `ORDER BY "FullName","BirthDate"` foi preservada: dois imports
concorrentes adquirem locks de índice na mesma ordem, reduzindo deadlock.

### 4.4 Duplicatas dentro do mesmo lote

`DISTINCT ON (...) ... ORDER BY ..., "ctid" DESC` foi mantido. Sem ele o PostgreSQL rejeitaria o
comando com *“ON CONFLICT DO UPDATE/DO NOTHING command cannot affect row a second time”*. A última
linha da staging vence, o que preserva a ordem de acumulação do import.

### 4.5 Divergência de atributos

Cenário: CPF já existe com `"FullName" = "MARIA DA SILVA"`, e uma importação posterior traz o mesmo
CPF com `"FullName" = "MARIA SILVA"`.

Comportamento implementado:

1. A identidade existente é **preservada** (o `DO NOTHING` não toca na linha).
2. O ID canônico é devolvido e usado pelo candidato — o vínculo não se perde.
3. É gerado um `CandidateIdentityConflict` e logado por
   `ValidationLog.CandidateIdentityConflict` (EventId 1308) em
   `src/Application/Bravo.Application/Logging/ValidationLog.cs`. A mensagem contém o
   `CandidateIdentityId` e quais atributos divergiram — **nunca o CPF**.
4. Divergências são deduplicadas por `CandidateIdentityId` dentro do lote, para não gerar uma
   mensagem por linha repetida da mesma fonte.

Optou-se por **não** inventar uma política de reconciliação (ex.: escolher o nome “mais completo”).
Isso é uma decisão de domínio que não existe hoje no código e não foi presumida.

### 4.6 Roundtrips: antes e depois

Medido com `tests/Bravo.IntegrationTests/Importing/SqlStatementCounter.cs`, que escuta o evento
Npgsql `2000` (“Executing command”) de um `NpgsqlDataSource` dedicado — ou seja, conta o que
realmente foi enviado.

| Operação | Antes | Depois |
|---|---|---|
| `CREATE TEMP TABLE` | 1 | 1 |
| `COPY` binário | 1 | 1 |
| Statement de merge/insert (CPF) | 1 | 1 |
| Statement de merge/insert (sem CPF) | 1 | 1 |
| Leitura dos IDs | 0 (vinha do `RETURNING`) | 1 |
| **Comandos contados pelo evento Npgsql** | **3** | **4** |

Ou seja: **um comando adicional por lote**, independentemente do tamanho do lote (teste:
`IdentityBatchCostsTheSameNumberOfStatementsForAnyBatchSize`, que compara lote de 1 com lote de 100
e exige o mesmo número). Em troca, **zero `UPDATE`**.

O `COPY` não aparece na contagem porque é uma operação de replicação e não dispara o evento de
comando do Npgsql; ele ocorre nos dois formatos.

Esse trade-off é o ponto central: no formato anterior o custo por identidade existente era uma
nova versão de linha; no formato novo é uma leitura indexada dentro do mesmo lote. Como o import de
candidatos reencontra as mesmas pessoas em vários anos e estados, o número de conflitos é
proporcional ao total de candidatos, enquanto o custo adicional é O(1) por lote.

---

## 5. Casos potenciais (exigem decisão antes de alterar)

### 5.1 `ElectoralSections` — `DO UPDATE` que só renova `UpdatedAt`

| Campo | Valor |
|---|---|
| **Repositório** | `ElectoralStructureRepository` |
| **Método** | `UpsertSectionsAsync` |
| **Destino** | `ElectoralSections` |
| **Comportamento atual** | `ON CONFLICT ("MunicipalityId","ZoneId","TseSectionNumber") DO UPDATE SET "UpdatedAt" = EXCLUDED."UpdatedAt"` |
| **Evidência** | `ElectoralStructureRepository.cs:82-95`, com comentário admitindo que “a section não tem coluna mutável”. |
| **Problema** | Não há atributo de negócio mutável. O `UPDATE` existe só para renovar o timestamp. O próprio código observa que o chamador já filtra seções existentes (`TseImportSource.Structure.cs:81-85`), então o ramo de conflito só é atingido por entrada genuinamente repetida. |
| **Impacto** | Provavelmente pequeno, porque o filtro no chamador evita o conflito na maioria dos casos. Mas em reimportação concorrente o filtro é apenas uma leitura anterior, não uma garantia. |
| **Correção proposta** | Trocar por `DO NOTHING`, **desde que** se confirme que `UpdatedAt` de `ElectoralSections` não é usado como sinal de vida em nenhum lugar (não há evidência de uso). |
| **Risco** | Perda do único efeito observável. Baixo, mas exige confirmação. |
| **Status** | **Potencial — aguardando aprovação** |

### 5.2 `ImportJobs` — progresso e heartbeat

| Campo | Valor |
|---|---|
| **Repositório** | `ImportJobRepository` |
| **Métodos** | `ReportProgressAsync`, `RenewLeaseAsync` |
| **Destino** | `ImportJobs` |
| **Comportamento atual** | `UPDATE` incondicional a cada flush. |
| **Evidência** | `ImportJobRepository.cs:92-115`. Throttle em `ImportProgressSink` (`ProgressFlushMilliseconds`, default 1000 ms) e intervalo de heartbeat de `DefaultLeaseDurationSeconds / 3`. |
| **Problema** | Duas escritas por ciclo (progresso + renovação de lease) que podem gravar valores idênticos. |
| **Impacto** | Uma linha, a cada poucos segundos, por job. Irrelevante em volume, mas é escrita em linha quente e pode gerar contenção com o scheduler. |
| **Correção proposta** | Combinar as duas em um único `UPDATE` no heartbeat, e adicionar guarda no progresso: `WHERE (target."CompletedOperations", target."TotalProcessedRecords", ...) IS DISTINCT FROM (EXCLUDED...)`. **Não** guardar o heartbeat: renovar o lease com valor igual ainda é intencional (prova de vida). |
| **Risco** | O heartbeat não pode ser suprimido — só o progresso redundante. |
| **Status** | **Potencial — aguardando aprovação** |

### 5.3 `DatasetVersions` — `SetStatusAsync` imediatamente após `CreateAsync`

| Campo | Valor |
|---|---|
| **Repositório** | `DatasetVersionRepository` |
| **Métodos** | `CreateAsync` e `SetStatusAsync` |
| **Destino** | `DatasetVersions` |
| **Comportamento atual** | `CreateAsync` insere com `Status = 'Pending'`; `DatasetVersionService.RegisterAsync` chama `SetStatusAsync(..., Importing)` logo em seguida (`DatasetVersionService.cs:38-49`). |
| **Problema** | Duas escritas para criar uma linha cujo estado final conhecido já é `Importing`. |
| **Impacto** | Duas linhas de escrita e dois roundtrips por escopo de resultado, que no TSE são 15 anos × 28 estados. |
| **Correção proposta** | Inserir já com o status `Importing`, ou fazer o `SetStatusAsync` acontecer antes do insert. |
| **Risco** | Baixo. O estado `Pending` talvez tenha significado para um consumidor externo; precisa de confirmação. |
| **Status** | **Potencial — aguardando aprovação** |

---

## 6. Casos legítimos que não devem ser alterados

Registrados aqui para evitar otimizações futuras incorretas.

### 6.1 `ImportFailedRecords` — `AttemptCount` é o efeito observável

`ImportFailedRecordRepository.MergeSql` (`ImportFailedRecordRepository.cs:49-70`) faz
`"AttemptCount" = target."AttemptCount" + 1` sem guarda. Isso está **correto**: incrementar a
contagem é exatamente o efeito de registrar a mesma falha novamente, e um guarda de no-op perderia
o histórico de tentativas. Há teste de integração cobrindo (`IdempotencyIntegrationTests`).

### 6.2 `Candidates`, `ElectionResults`, `Coalitions`, `PollingPlaces`, `TseMunicipalityCodes`

Todos já possuem guarda `WHERE ROW(...) IS DISTINCT FROM ROW(...)` sobre as colunas mutáveis e
excluem `UpdatedAt` da comparação. Exemplos:

- `CandidateRepository.MergeCandidatesSql:153-161`
- `ElectionResultRepository.MergeSql:74`
- `CoalitionRepository.MergeSql:55-56`
- `ElectoralStructureRepository.MergePollingPlacesSql:150-157`
- `TseMunicipalityCodeRepository.MergeSql:63-68`

### 6.3 `States`, `Municipalities`, `ElectoralOffices`, `PoliticalParties`, `Federations`

Idem: guarda de no-op presente. `GeographyLookup.MergeStatesSql:52-53`,
`MergeMunicipalitiesSql:93-94`, `ElectoralReferenceRepository` linhas 59, 92 e 130-131.

### 6.4 `UserRepository.Update` (EF Core)

`UpdateUserHandler` e `LoginHandler` só chamam `Update` + `Commit` quando há mudança real de estado
(`RehashPassword` ou `Update` do usuário). Não há escrita incondicional. `PostgreSQLContext`
atualiza `UpdatedAt` em `SaveChangesAsync` para entidades `Modified`, o que é a semântica esperada do
EF Core.

### 6.5 Defeito encontrado fora do escopo (registrado, não corrigido)

Durante a validação apareceu uma falha de teste **não relacionada** a escritas:
`TseHttpClient.CopyWithChecksumAsync` (`src/Infrastructure/Bravo.Infrastructure.Tse/Clients/TseHttpClient.cs:175-200`)
tem `long total = 0` mas a linha de acumulação foi removida na alteração de trabalho em andamento.
Por isso `AcquiredDataset.SizeBytes` é sempre 0 e `EnsureWithinLimit` está inoperante (corpo
comentado). Não afeta nenhuma escrita em banco, mas quebra
`tests/Bravo.UnitTests/Tse/TseHttpClientTests.DownloadsFileAndComputesSha256` e desativa o limite de
download. `MaxDownloadBytes` deve ser restaurado junto com a acumulação.

---

## 7. Observações estruturais

1. **O helper `BulkCopy` é o único ponto de transporte.** Ele não decide semântica de negócio; abre
   conexão, cria a staging `ON COMMIT DROP`, copia e delega o merge. Isso é bom e deve ser mantido:
   qualquer correção de escrita deve acontecer no SQL do repositório, não no helper.
2. **`ON CONFLICT DO UPDATE` com `RETURNING` é a armadilha recorrente.** O `RETURNING` só devolve
   linhas realmente escritas. Sempre que um chamador precisa do ID de uma linha existente, a
   tentação é usar `DO UPDATE` para “forçar” o retorno. **Isso vale inclusive com guarda:** um
   `WHERE` que suprime o update também suprime o `RETURNING` (verificado em PostgreSQL 16). O padrão
   correto é `DO NOTHING` + leitura, como foi feito em `CandidateIdentities`.
   **`ElectionRepository` ainda usa esse padrão** e é o próximo candidato natural.
3. **`UpdatedAt` é tratado de duas formas inconsistentes.** Nos repositórios migrados, ele é
   excluído da comparação e não é renovado quando nada muda. Em `Elections`, `ElectionRounds`,
   `ImportDatasets` e `ElectoralSections`, ele ainda é renovado incondicionalmente. Unificar essa
   semântica é pré-requisito para as correções 3.2, 3.3 e 5.1.
4. **Não há triggers nem auditoria.** Qualquer decisão baseada em “o trigger pode reagir ao
   UPDATE” não se aplica a este esquema, conforme verificado nas migrations.

---

## 8. Trabalho pendente de aprovação

Nenhum outro repositório foi modificado. A lista abaixo é a ordem sugerida de implementação, da
menor para a maior necessidade de decisão:

1. `ElectionRepository` (3.2) — ganho certo, risco quase nulo, mantém `RETURNING`.
2. `DatasetVersionRepository` (5.3) — dois roundtrips evitados por escopo de resultado.
3. `ElectoralStructureRepository` / seções (5.1) — depende de confirmar que `UpdatedAt` não é sinal
   de vida.
4. `ImportJobRepository` / progresso (5.2) — depende de desenho do heartbeat.
5. `ImportDatasetRepository` (3.3) — depende de decisão sobre `LastSeenAt`.

---

## 9. Referências de código

| Assunto | Arquivo |
|---|---|
| Entidade imutável | `src/Domain/Bravo.Domain/Electoral/Entities/CandidateIdentity.cs` |
| Chave natural | `src/Domain/Bravo.Domain/Electoral/Services/CandidateIdentityKey.cs` |
| Resultado da resolução | `src/Domain/Bravo.Domain/Electoral/Models/CandidateIdentityResolution.cs` |
| Contrato do repositório | `src/Domain/Bravo.Domain/Electoral/Repositories/ICandidateRepository.cs` |
| Implementação | `src/Infrastructure/Bravo.Infrastructure.PostgreSQL.Dapper/Repositories/Electoral/CandidateRepository.cs` |
| Helper de transporte | `src/Infrastructure/Bravo.Infrastructure.PostgreSQL.Dapper/Bulk/BulkCopy.cs` |
| Índices | `src/Infrastructure/Bravo.Infrastructure.PostgreSQL/Migrations/20261120225129_FixUniqueIndexNullSemantics.cs` |
| Mapeamento EF | `src/Infrastructure/Bravo.Infrastructure.PostgreSQL/Mappings/ElectoralMaps/CandidateIdentityMap.cs` |
| Log de conflito | `src/Application/Bravo.Application/Logging/ValidationLog.cs` |
| Testes de imutabilidade | `tests/Bravo.IntegrationTests/Importing/CandidateIdentityImmutabilityTests.cs` |
| Medidor de roundtrips | `tests/Bravo.IntegrationTests/Importing/SqlStatementCounter.cs` |
