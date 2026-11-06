using System.Net;

namespace Datavello.Api.Infrastructure.Email;

/// <summary>Transactional e-mails in pt-BR: plain text plus a simple HTML version (user data is HTML-encoded).</summary>
public static class EmailTemplates
{
    public static EmailMessage PasswordReset(string to, string fullName, string link, TimeSpan validFor) =>
        Build(
            to,
            subject: "Redefinição de senha — Datavello",
            greeting: $"Olá, {fullName}!",
            paragraphs:
            [
                "Recebemos um pedido para redefinir a senha da sua conta no Datavello.",
                $"Use o botão abaixo para criar uma nova senha. O link vale por {Describe(validFor)} e só pode ser usado uma vez."
            ],
            buttonText: "Criar nova senha",
            link: link,
            footer: "Se você não pediu a redefinição, ignore este e-mail: sua senha continua a mesma.");

    public static EmailMessage Invitation(string to, string companyName, string invitedBy, string link, DateTimeOffset expiresAt) =>
        Build(
            to,
            subject: $"{invitedBy} convidou você para {companyName} no Datavello",
            greeting: "Olá!",
            paragraphs:
            [
                $"{invitedBy} convidou você para acessar os dados da empresa {companyName} no Datavello, a ferramenta de indicadores e dashboards da empresa.",
                $"Use o botão abaixo para aceitar. O convite vale até {FormatDate(expiresAt)}, só pode ser usado uma vez e somente com este e-mail ({to})."
            ],
            buttonText: "Ver convite",
            link: link,
            footer: "Se você não esperava este convite, ignore este e-mail.");

    public static EmailMessage ConfirmEmailChange(string to, string fullName, string link, TimeSpan validFor) =>
        Build(
            to,
            subject: "Confirme seu novo e-mail — Datavello",
            greeting: $"Olá, {fullName}!",
            paragraphs:
            [
                $"Recebemos um pedido para usar {to} como e-mail da sua conta no Datavello.",
                $"Confirme pelo botão abaixo. O link vale por {Describe(validFor)}; até a confirmação, o e-mail atual continua valendo."
            ],
            buttonText: "Confirmar novo e-mail",
            link: link,
            footer: "Se você não fez esse pedido, ignore este e-mail: nada será alterado.");

    public static EmailMessage EmailChangedNotice(string to, string fullName, string newEmail, string supportLink) =>
        Build(
            to,
            subject: "O e-mail da sua conta foi alterado — Datavello",
            greeting: $"Olá, {fullName}!",
            paragraphs:
            [
                $"O e-mail da sua conta no Datavello foi alterado para {newEmail}. A partir de agora, use o novo endereço para entrar.",
                "Por segurança, as sessões abertas foram encerradas."
            ],
            buttonText: "Entrar no Datavello",
            link: supportLink,
            footer: "Se não foi você, entre em contato com o proprietário da sua empresa no Datavello imediatamente.");

    private static readonly TimeZoneInfo Brasilia = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    private static string FormatDate(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, Brasilia).ToString("dd/MM/yyyy");

    private static string Describe(TimeSpan span) =>
        span.TotalHours >= 1 && span.TotalHours % 1 == 0
            ? span.TotalHours == 1 ? "1 hora" : $"{span.TotalHours:0} horas"
            : $"{span.TotalMinutes:0} minutos";

    private static EmailMessage Build(string to, string subject, string greeting, string[] paragraphs, string buttonText, string link, string footer)
    {
        var text = string.Join("\n\n", [greeting, .. paragraphs, $"{buttonText}: {link}", footer, "— Datavello · Dados claros. Decisões melhores."]);

        string E(string s) => WebUtility.HtmlEncode(s);
        var html = $"""
            <!doctype html>
            <html lang="pt-BR">
            <body style="margin:0;padding:24px;background:#f5f7fb;font-family:Segoe UI,Arial,sans-serif;color:#1f2937">
              <table role="presentation" width="100%" style="max-width:520px;margin:0 auto;background:#ffffff;border:1px solid #e5e7eb;border-radius:12px">
                <tr><td style="padding:28px">
                  <p style="margin:0 0 20px;font-size:18px;font-weight:600;color:#1458c9">Datavello</p>
                  <p style="margin:0 0 16px;font-size:15px">{E(greeting)}</p>
                  {string.Concat(paragraphs.Select(p => $"<p style=\"margin:0 0 16px;font-size:15px;line-height:1.5\">{E(p)}</p>"))}
                  <p style="margin:24px 0">
                    <a href="{E(link)}" style="display:inline-block;padding:10px 18px;background:#1458c9;color:#ffffff;text-decoration:none;border-radius:8px;font-weight:600">{E(buttonText)}</a>
                  </p>
                  <p style="margin:0 0 16px;font-size:13px;color:#6b7280;line-height:1.5">Se o botão não funcionar, copie este endereço no navegador:<br><span style="word-break:break-all">{E(link)}</span></p>
                  <p style="margin:0;font-size:13px;color:#6b7280">{E(footer)}</p>
                </td></tr>
              </table>
              <p style="text-align:center;font-size:12px;color:#9ca3af">Dados claros. Decisões melhores.</p>
            </body>
            </html>
            """;

        return new EmailMessage(to, subject, text, html);
    }
}
