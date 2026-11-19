using Bravo.Application.Extensions;
using Bravo.Infrastructure.Email.Extensions;
using Bravo.Presentation.Extensions;
using Bravo.Presentation.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddRollingFile(builder.Configuration);

builder.Services.AddDatabase(builder.Configuration)
                .AddSecurity(builder.Configuration)
                .AddEmailSender(builder.Configuration)
                .AddControllerConfiguration()
                .AddDocumentation(builder.Environment)
                .AddApplication()
                .AddImportWorker(builder.Configuration)
                .AddExceptions();

var app = builder.Build();

app.UseExceptionHandler()
    .UseDatabase()
   .UseSecurity()
   .UseDocumentation()
   .UseApplication()
   .UseStaticFiles();

app.Run();
