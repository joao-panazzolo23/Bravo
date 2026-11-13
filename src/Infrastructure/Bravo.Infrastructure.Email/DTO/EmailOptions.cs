using MailKit.Security;

namespace Bravo.Infrastructure.Email.DTO;

internal class EmailOptions
{
    public string Email { get; set; }
    public string Host { get; set; }
    public int Port { get; set; }
    public string User { get; set; }
    public string Pass { get; set; }
    public SecureSocketOptions Security { get; internal set; } = SecureSocketOptions.StartTls;
}


//    "Email": {
//  "Email": "envio@wearenomad.dev",
//  "Host": "d.smtp.cipnet.com.br",
//  "Port": "587",
//  "User": "envio@wearenomad.dev",
//  "Pass": "Dv3Rh1Xh@7abW"
//},