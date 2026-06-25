# SLC-AS-SendEmailWhenErrorInLog

## About

**SLC-AS-SendEmailWhenErrorInLog** is a DataMiner Automation script that scans a log file for one or more error signatures and sends an alert email when the error is detected. The alert email lists the log lines that matched, making it easy to spot what went wrong.

The script is fully parameterized, so the same script can be reused for any log file, error text, recipient list, and SMTP configuration without code changes.

## Key Features

- **Log file scanning**: Reads any file on the DataMiner Agent and checks it for the configured error text.
- **Multi-signature AND matching**: Accepts multiple error strings (separated by `;`). An email is sent only when **every** string is present in the file (case-insensitive).
- **Detailed alerts**: The email body includes the log file path and every log line that matched an error signature.
- **Multiple recipients**: Supports sending the alert to one or more email addresses.
- **Non-intrusive read**: The log file is opened with shared read/write access, so the live log is never locked while DataMiner keeps writing to it.

## How It Works

1. The script reads its input from the script parameters and validates that all required values are provided.
2. It verifies that the configured log file exists and reads its content.
3. It checks whether **all** configured error signatures are present in the file (case-insensitive AND match).
4. If the error is found, it builds an email containing the matching log lines and sends it through the configured SMTP host. If no error is found, no email is sent.

## Parameters

The script is driven entirely by the following script parameters:

| Parameter | Description | Example |
|-----------|-------------|---------|
| **Log File Path** | Full path of the file to scan. | `C:\Skyline DataMiner\Logging\ElementABC.txt` |
| **Error Text** | One or more strings to look for, separated by `;`. An email is sent only when **every** string is present in the file (case-insensitive AND match). | `Issue found while importing DMS services` |
| **Email Recipients** | One or more recipient email addresses, separated by `;`. | `someone.else@skyline.be` |
| **From Address** | The sender address of the alert email. | `dataminer_notification@skyline.be` |
| **SMTP Host** | The SMTP server used to send the email. | `mail.skyline.be` |
| **Email Subject** | The subject line of the alert email. | `Error detected in Elemment ABC log` |

> [!NOTE]
> The SMTP port is fixed at `25` in the script. Adjust the source if a different port is required.

## Prerequisites

- A DataMiner System where the Automation script can run.
- Read access to the log file on the DataMiner Agent.
- A reachable SMTP host that accepts mail from the configured sender address.

## Usage

1. Deploy the package to your DataMiner System.
2. Run the **SLC-AS-SendEmailWhenErrorInLog** Automation script, providing values for the parameters listed above (for example, through a scheduled task or another script).
3. When all configured error signatures are found in the log file, an alert email is sent to the configured recipients. Progress and results are reported as information events in DataMiner

