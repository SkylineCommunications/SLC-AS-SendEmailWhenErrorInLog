using Skyline.DataMiner.Automation;
using System;
using System.Linq;
using System.IO;
using System.Net.Mail;

namespace SLCASSendEmailWhenErrorInLog
{
    /// <summary>
    /// DataMiner Automation script that scans a log file and sends an alert email
    /// when a specific error is found.
    /// </summary>
    /// <remarks>
    /// All inputs are provided through script parameters, so the script is reusable
    /// for any log file, error text, recipient list and SMTP configuration without
    /// code changes:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// <b>Log File Path</b>: full path of the file to scan,
    /// e.g. <c>C:\Skyline DataMiner\Logging\Helios DB.txt</c>.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>Error Text</b>: one or more strings to look for, separated by <c>;</c>,
    /// e.g. <c>Issue found while importing DMS services;Duplicate property</c>.
    /// An email is sent only when <b>every</b> string is present in the file
    /// (case-insensitive AND match).
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>Email Recipients</b>: one or more email addresses, separated by <c>;</c>,
    /// e.g. <c>rafael.duarte@skyline.be;someone.else@skyline.be</c>.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>From Address</b>: the sender address of the alert email,
    /// e.g. <c>dataminer_notification@skyline.be</c>.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>SMTP Host</b>: the SMTP server used to send the email,
    /// e.g. <c>mail.skyline.be</c>.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>Email Subject</b>: the subject line of the alert email.
    /// </description>
    /// </item>
    /// </list>
    /// The alert email lists the log lines that matched. The SMTP port
    /// (<see cref="SmtpPort"/>) is kept as a constant. The file is opened with
    /// <see cref="System.IO.FileShare.ReadWrite"/> so the live log is not locked
    /// while DataMiner keeps writing to it.
    /// </remarks>
    public class Script
    {
        // Parameter names (must match the Description of each ScriptParameter in the XML).
        private const string ParamLogFilePath = "Log File Path";
        private const string ParamErrorText = "Error Text";
        private const string ParamRecipients = "Email Recipients";
        private const string ParamFromAddress = "From Address";
        private const string ParamSmtpHost = "SMTP Host";
        private const string ParamMailSubject = "Email Subject";

        // SMTP port (kept fixed; parametrize the same way if needed).
        private const int SmtpPort = 25;

        private const string LogPrefix = "[SendEmailWhenError]";

        /// <summary>
        /// The script entry point.
        /// </summary>
        /// <param name="engine">Link with SLAutomation process.</param>
        public void Run(IEngine engine)
        {
            try
            {
                RunSafe(engine);
            }
            // Let DataMiner handle its own lifecycle exceptions.
            catch (ScriptAbortException) { throw; }
            catch (ScriptForceAbortException) { throw; }
            catch (ScriptTimeoutException) { throw; }
            catch (InteractiveUserDetachedException) { throw; }
            catch (Exception e)
            {
                engine.ExitFail($"{LogPrefix} Something went wrong: {e}");
            }
        }

        private void RunSafe(IEngine engine)
        {
            engine.GenerateInformation($"{LogPrefix} Script started.");

            // Read the script parameters.
            string logFilePath = GetParam(engine, ParamLogFilePath);
            string[] errorSignatures = SplitList(GetParam(engine, ParamErrorText));
            string[] recipients = SplitList(GetParam(engine, ParamRecipients));
            string fromAddress = GetParam(engine, ParamFromAddress);
            string smtpHost = GetParam(engine, ParamSmtpHost);
            string mailSubject = GetParam(engine, ParamMailSubject);

            // Validate the parameters.
            if (string.IsNullOrWhiteSpace(logFilePath))
            {
                engine.ExitFail($"{LogPrefix} The '{ParamLogFilePath}' parameter is empty.");
                return;
            }

            if (errorSignatures.Length == 0)
            {
                engine.ExitFail($"{LogPrefix} The '{ParamErrorText}' parameter is empty.");
                return;
            }

            if (recipients.Length == 0)
            {
                engine.ExitFail($"{LogPrefix} The '{ParamRecipients}' parameter is empty.");
                return;
            }

            if (string.IsNullOrWhiteSpace(fromAddress))
            {
                engine.ExitFail($"{LogPrefix} The '{ParamFromAddress}' parameter is empty.");
                return;
            }

            if (string.IsNullOrWhiteSpace(smtpHost))
            {
                engine.ExitFail($"{LogPrefix} The '{ParamSmtpHost}' parameter is empty.");
                return;
            }

            if (string.IsNullOrWhiteSpace(mailSubject))
            {
                engine.ExitFail($"{LogPrefix} The '{ParamMailSubject}' parameter is empty.");
                return;
            }

            if (!File.Exists(logFilePath))
            {
                engine.ExitFail($"{LogPrefix} File not found: '{logFilePath}'.");
                return;
            }

            string content = ReadFile(logFilePath);

            // Only trigger when every error text is present in the file.
            bool errorFound = errorSignatures.All(
                signature => content.IndexOf(signature, StringComparison.OrdinalIgnoreCase) >= 0);

            if (!errorFound)
            {
                engine.GenerateInformation($"{LogPrefix} No error found. No email sent.");
                return;
            }

            engine.GenerateInformation($"{LogPrefix} Error found. Sending email...");
            SendEmail(logFilePath, errorSignatures, recipients, content, fromAddress, smtpHost, mailSubject);
            engine.GenerateInformation($"{LogPrefix} Email sent.");
        }

        private static void SendEmail(
            string logFilePath,
            string[] errorSignatures,
            string[] recipients,
            string logContent,
            string fromAddress,
            string smtpHost,
            string mailSubject)
        {
            string matchingLines = string.Join(
                "\r\n",
                logContent
                    .Split('\n')
                    .Select(line => line.Trim())
                    .Where(line => errorSignatures.Any(s => line.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)));

            string body =
                "Hello,\r\n\r\n" +
                $"An error was detected in the log file:\r\n{logFilePath}\r\n\r\n" +
                $"Matching lines:\r\n{matchingLines}\r\n\r\n" +
                "Regards,\r\nDataMiner";

            using (var client = new SmtpClient(smtpHost, SmtpPort))
            using (var mail = new MailMessage())
            {
                mail.From = new MailAddress(fromAddress);
                mail.Subject = mailSubject;
                mail.Body = body;

                foreach (string recipient in recipients)
                {
                    mail.To.Add(new MailAddress(recipient));
                }

                client.Send(mail);
            }
        }

        private static string ReadFile(string path)
        {
            // FileShare.ReadWrite so the live log stays readable while DataMiner writes to it.
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        private static string GetParam(IEngine engine, string name)
        {
            ScriptParam param = engine.GetScriptParam(name);
            return param?.Value?.Trim() ?? string.Empty;
        }

        private static string[] SplitList(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return Array.Empty<string>();
            }

            return raw
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .Where(value => value.Length > 0)
                .ToArray();
        }
    }
}
