using Bravo.Application.Extensions;
using Bravo.Infrastructure.Email.Extensions;
using Bravo.Presentation.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDatabase(builder.Configuration)
                .AddSecurity(builder.Configuration)
                .AddEmailSender(builder.Configuration)
                .AddControllerConfiguration()
                .AddDocumentation(builder.Environment)
                .AddApplication()
                .AddExceptions();

var app = builder.Build();

app.UseExceptionHandler()
   .UseSecurity()
   .UseDocumentation()
   .UseApplication()
   .UseStaticFiles();

app.Run();