namespace DataKavach.Services;

/// <summary>
/// Policy/notice templates. Markup: "# " heading, "## " sub-heading, "- " bullet, blank line = new paragraph.
/// Placeholders are filled from the organisation profile. Generated output is always a DRAFT requiring legal review.
/// </summary>
public static class PolicyTemplates
{
    public sealed record Tpl(string Key, string Title, string Body);

    const string Footer = """

        ## Document control
        This document was generated as a draft by Data Kavach from a template on {{Date}}. It must be reviewed and approved by {{Org}}'s legal counsel before adoption. It is not legal advice and is not a legally approved policy until formally approved.
        """;

    public static readonly Tpl[] All =
    {
        new("data-protection-policy", "Data Protection Policy", """
            # Data Protection Policy
            ## 1. Purpose and scope
            This policy sets out how {{Org}} ("the Company") complies with the Digital Personal Data Protection Act, 2023 ("the Act") and the Digital Personal Data Protection Rules, 2025 ("the Rules") when processing digital personal data of employees, customers, vendors, contractors, website visitors and other Data Principals. It applies to all employees, contractors and Data Processors acting on the Company's behalf.

            Where existing Company policies (HR, information security, vendor management, records retention) already govern a topic, those policies continue to apply and are amended to include the DPDP clauses referenced below, rather than being replaced.
            ## 2. Accountability
            The Company is responsible for compliance in respect of all processing undertaken by it or on its behalf by a Data Processor (s.8(1)). {{DpoName}} is the accountable owner for DPDP compliance and reports to senior management / the Board.
            ## 3. Grounds for processing
            Personal data is processed only for a lawful purpose (s.4), either:
            - with the free, specific, informed, unconditional and unambiguous consent of the Data Principal (s.6); or
            - for a legitimate use under s.7, including employment-related purposes under s.7(i).
            Each processing activity and its ground is recorded in the Personal Data Inventory.
            ## 4. Notice and consent
            Every consent request is accompanied or preceded by a standalone notice in clear and plain language that itemises the personal data and purposes and explains how to withdraw consent, exercise rights and complain to the Data Protection Board (s.5, Rule 3). Withdrawal is as easy as giving consent (s.6(4)).
            ## 5. Data quality, retention and erasure
            - Personal data used for decisions or disclosed to another Data Fiduciary is kept complete, accurate and consistent (s.8(3)).
            - Personal data is erased when consent is withdrawn or the specified purpose is no longer served, unless retention is required by law (s.8(7)); Data Processors are required to erase as well.
            - Processing logs and associated personal data are retained for at least one year (Rule 8(3)) and security logs for one year (Rule 6(1)(e)).
            ## 6. Reasonable security safeguards
            The Company implements the safeguards in Rule 6, including encryption/masking/tokenisation, access control, logging and monitoring, backups, one-year log retention, safeguard clauses in processor contracts, and organisational measures. Details are in the Information Security Policy.
            ## 7. Personal data breach
            Breaches are handled under the Data Breach Response Policy. Affected Data Principals and the Data Protection Board are intimated without delay, and a detailed report is provided to the Board within 72 hours of becoming aware (Rule 7). CERT-In and sectoral reporting obligations are handled separately from the same incident record.
            ## 8. Rights of Data Principals and grievances
            Data Principals may request access, correction, completion, updating, erasure, nominate another individual, and raise grievances (s.11-14). Requests are made at {{GrievanceUrl}} or {{PrivacyEmail}}. Grievances are responded to within {{GrievanceDays}} days (Rule 14(3) - not exceeding 90 days).
            ## 9. Data Processors
            Processors are engaged only under a valid contract (s.8(2)) containing security safeguards, breach notification, erasure and sub-processor obligations.
            ## 10. Children and persons with disability
            Where applicable, verifiable consent of the parent or lawful guardian is obtained before processing (s.9, Rules 10-11), and the Company does not track, behaviourally monitor or target advertising at children.
            ## 11. Contact
            {{DpoName}}, {{DpoDesignation}} - {{DpoEmail}} / {{DpoPhone}}
            ## 12. Review
            This policy is reviewed at least annually and on material change to the law or the Company's processing.
            """ + Footer),
        new("privacy-notice", "Privacy Notice", """
            # Privacy Notice
            {{Org}} ("we") processes your personal data in accordance with the Digital Personal Data Protection Act, 2023 and the Digital Personal Data Protection Rules, 2025.
            ## Personal data we process
            - [Itemise each data element, e.g. name, mobile number, email address, address, payment details]
            ## Why we process it
            - [Specific purpose 1 and the goods/services it enables]
            - [Specific purpose 2]
            ## Withdrawing consent
            You may withdraw your consent at any time at {{GrievanceUrl}} or by writing to {{PrivacyEmail}}. Withdrawal is as easy as giving consent. Withdrawal does not affect processing carried out before withdrawal.
            ## Your rights
            You may request a summary of your personal data and processing, correction, completion, updating or erasure of your data, and nominate another individual to exercise your rights in the event of death or incapacity. Use {{GrievanceUrl}} and quote your [customer ID / registered email / mobile number].
            ## Grievances
            Contact {{DpoName}} at {{DpoEmail}}. We respond within {{GrievanceDays}} days. You must first use our grievance mechanism before approaching the Board (s.13(3)).
            ## Complaint to the Data Protection Board of India
            If you are not satisfied, you may complain to the Data Protection Board of India through its digital platform.
            ## Language
            This notice is available in English and [Eighth Schedule languages offered].
            """ + Footer),
        new("employee-privacy-notice", "Employee Privacy Notice", """
            # Employee Privacy Notice
            {{Org}} processes personal data of employees and applicants for purposes of employment and to safeguard the Company from loss or liability, which is a legitimate use under s.7(i) of the DPDP Act. Consent is sought only where processing goes beyond these purposes.
            ## Personal data processed
            - Identity and contact details, KYC documents, education and employment history
            - Payroll, bank account, statutory (PF/ESI/tax) details
            - Attendance, performance, IT and access logs
            ## Purposes
            - Recruitment, onboarding, payroll, benefits, statutory compliance, performance management, IT security monitoring and exit formalities.
            ## Sharing
            Payroll processors, insurers, background verification agencies and statutory authorities, under contracts with security safeguards.
            ## Retention
            As per the HR records retention schedule and applicable labour, tax and company law.
            ## Your rights and contact
            Contact HR or {{DpoName}} ({{DpoEmail}}) to access, correct or update your data or raise a grievance.
            """ + Footer),
        new("customer-privacy-notice", "Customer Privacy Notice", """
            # Customer Privacy Notice
            This notice explains how {{Org}} processes personal data of customers when providing our products and services.
            ## Data and purposes
            - Account and contact data - to provide the service, communicate and support you.
            - Transaction data - to process orders, payments and invoices.
            - [Marketing data] - only with your separate consent, which you may withdraw at any time.
            ## Withdrawal, rights and complaints
            Withdraw consent or exercise your rights at {{GrievanceUrl}}. Grievances: {{DpoEmail}} (response within {{GrievanceDays}} days). You may complain to the Data Protection Board of India.
            """ + Footer),
        new("vendor-clause", "Vendor Data Protection Clause", """
            # Data Protection Clause for Vendors / Data Processors
            ## 1. Processing on instructions
            The Processor shall process personal data made available by {{Org}} ("the Fiduciary") only on the Fiduciary's documented instructions and for the purposes of this Agreement.
            ## 2. Security safeguards
            The Processor shall implement reasonable security safeguards at least equivalent to Rule 6 of the DPDP Rules, 2025, including encryption/masking, access control, logging and monitoring with one-year log retention, backups and organisational measures.
            ## 3. Personal data breach
            The Processor shall notify the Fiduciary of any personal data breach without undue delay and in any event within [24] hours of becoming aware, with all information required for the Fiduciary to comply with Rule 7.
            ## 4. Erasure
            On withdrawal of consent, completion of purpose or termination, the Processor shall erase (and cause its sub-processors to erase) the personal data and certify erasure in writing, subject to the minimum retention of logs required by Rule 8(3).
            ## 5. Sub-processors and transfers
            The Processor shall not engage sub-processors or transfer personal data outside India without prior written approval, and only in compliance with s.16 of the Act.
            ## 6. Assistance and audit
            The Processor shall assist the Fiduciary in responding to Data Principal requests and permit audits and inspections.
            """ + Footer),
        new("retention-policy", "Data Retention Policy", """
            # Data Retention Policy
            ## Principle
            {{Org}} retains personal data only as long as necessary for the specified purpose or as required by law (s.8(7)).
            ## Minimum retention
            - Processing logs, traffic data and associated personal data: minimum one year from processing (Rule 8(3)).
            - Security logs: one year (Rule 6(1)(e)); CERT-In also requires 180 days of ICT logs within India.
            ## Retention schedule
            - [Data category] - [retention period] - [legal basis for retention] - [erasure method]
            ## Erasure
            Erasure is performed by secure deletion or anonymisation and recorded. Processors are instructed to erase and certify.
            ## Third Schedule (if applicable)
            Where the Company is a Third Schedule class, data is erased three years after the Data Principal last approached the Company, with notice at least 48 hours before erasure (Rule 8(1)-(2)).
            """ + Footer),
        new("infosec-policy", "Information Security Policy (DPDP addendum)", """
            # Information Security Policy - DPDP Addendum
            This addendum amends the existing Information Security Policy to align with s.8(5) of the DPDP Act and Rule 6.
            ## Required safeguards
            - Encryption at rest and in transit; masking or tokenisation where full values are not needed (Rule 6(1)(a)).
            - Access control, MFA and privileged access management (Rule 6(1)(b)).
            - Central logging, monitoring and review of access to personal data (Rule 6(1)(c)).
            - Backups and restore testing for continuity (Rule 6(1)(d)).
            - Retention of logs and personal data for one year for investigation (Rule 6(1)(e)).
            - Security clauses in all processor contracts (Rule 6(1)(f)).
            - Training, vulnerability and patch management (Rule 6(1)(g)).
            ## Validation
            A control is considered effective only when its configuration is evidenced and validated; installing a tool is not sufficient.
            """ + Footer),
        new("breach-policy", "Data Breach Response Policy", """
            # Data Breach Response Policy
            ## Roles (one-page playbook)
            - Decides: Incident owner with {{DpoName}} and legal counsel.
            - Reports to the Board and CERT-In: {{DpoName}} / CISO.
            - Informs Data Principals: Customer communications team using approved templates.
            ## Timeline from becoming aware
            - Contain and assess immediately; open an incident record.
            - Intimate the Data Protection Board without delay (Rule 7(2)(a)).
            - Intimate each affected Data Principal without delay through the user account or registered communication mode (Rule 7(1)).
            - Detailed report to the Board within 72 hours, or longer period allowed by the Board on written request (Rule 7(2)(b)).
            - CERT-In report within 6 hours of noticing reportable cyber incidents (CERT-In Directions, 2022) - separate obligation.
            - Sectoral regulator reports as applicable.
            ## Evidence
            Every action, decision and notification is recorded with timestamps in the incident record.
            ## Drills
            A mock breach drill is run at least annually and timed against 72 hours.
            """ + Footer),
        new("consent-procedure", "Consent Management Procedure", """
            # Consent Management Procedure
            ## Capture
            - Present the notice before or with the consent request; separate purposes; no pre-ticked boxes.
            - Record identifier, purpose, notice version, language, timestamp and channel for every consent.
            ## Withdrawal
            - Provide withdrawal through the same channel with comparable ease.
            - Propagate withdrawal to all systems and processors within [x] working days and record completion.
            ## Proof
            - Retain consent records to prove notice and consent (s.6(10)).
            ## Consent Managers
            - Accept consent via Board-registered Consent Managers where integrated (s.6(7)-(9), Rule 4).
            """ + Footer),
        new("rights-procedure", "Data Principal Rights Procedure", """
            # Data Principal Rights Procedure
            ## Channels
            Requests are received at {{GrievanceUrl}} and {{PrivacyEmail}} and logged in the rights register.
            ## Steps
            - Log and acknowledge the request.
            - Verify identity using the identifiers required under our terms of service.
            - Locate data using the Personal Data Inventory; coordinate with processors.
            - Respond: access summary (s.11), correction/completion/update (s.12(2)), erasure unless retention is required (s.12(3)), nomination (s.14).
            - Include the DPO/contact details in every response (Rule 9).
            - Record closure evidence.
            ## Timelines
            Target response: [30] days; grievances within {{GrievanceDays}} days (maximum 90 days, Rule 14(3)).
            """ + Footer),
        new("erasure-procedure", "Data Erasure Procedure", """
            # Data Erasure Procedure
            ## Triggers
            - Consent withdrawn; purpose no longer served; erasure request; end of retention period; Third Schedule inactivity (if applicable).
            ## Exceptions
            - Retention required by law; minimum one-year retention of processing logs (Rule 8(3)).
            ## Method
            - Secure deletion from production, backups (on rotation) and processors; anonymisation where deletion is not feasible.
            - Record date, scope, method, approver and processor certificates.
            """ + Footer),
        new("grievance-procedure", "Grievance Redressal Procedure", """
            # Grievance Redressal Procedure
            ## Mechanism
            Data Principals may raise grievances regarding any act or omission of {{Org}} relating to their personal data or rights (s.13(1)) at {{GrievanceUrl}} or {{DpoEmail}}.
            ## Response period
            Grievances are responded to within {{GrievanceDays}} days of receipt. This period is published on our website and does not exceed 90 days (Rule 14(3)).
            ## Escalation
            Data Principals must exhaust this mechanism before approaching the Data Protection Board of India (s.13(3)).
            ## Records
            All grievances, responses and closure evidence are recorded and reviewed monthly by {{DpoName}}.
            """ + Footer),
    };

    public static string Fill(string body, Dictionary<string, object?> org)
    {
        string V(string k, string fallback) => DataKavach.Data.Db.Str(org.GetValueOrDefault(k)) is { Length: > 0 } s ? s : fallback;
        return body
            .Replace("{{Org}}", V("Name", "[Organisation]"))
            .Replace("{{DpoName}}", V("DpoName", "[DPO / Privacy Officer]"))
            .Replace("{{DpoDesignation}}", V("DpoDesignation", "[Designation]"))
            .Replace("{{DpoEmail}}", V("DpoEmail", V("PrivacyContactEmail", "[privacy email]")))
            .Replace("{{DpoPhone}}", V("DpoPhone", "[phone]"))
            .Replace("{{PrivacyEmail}}", V("PrivacyContactEmail", V("DpoEmail", "[privacy email]")))
            .Replace("{{GrievanceUrl}}", V("GrievanceUrl", V("Website", "[rights page URL]")))
            .Replace("{{GrievanceDays}}", V("GrievanceResponseDays", "30"))
            .Replace("{{Date}}", DateTime.UtcNow.ToString("dd MMM yyyy"));
    }
}
