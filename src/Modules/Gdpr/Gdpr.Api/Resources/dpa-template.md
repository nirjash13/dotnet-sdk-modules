# Data Processing Agreement

**Between:** {{CONTROLLER_NAME}} ("Controller")
**And:** {{PROCESSOR_NAME}} ("Processor")
**Date:** {{DATE}}

## 1. Subject-Matter and Duration

The Processor shall process personal data on behalf of the Controller for the purpose of providing the SaaS service described in the main agreement. Processing shall continue for the duration of the main agreement.

## 2. Nature and Purpose of Processing

Processing activities include: user account management, billing, communications, analytics, and product functionality as required by the Controller.

## 3. Categories of Data Subjects

End users of the Controller's platform, including employees, customers, and other individuals.

## 4. Categories of Personal Data

- Contact information (name, email, phone)
- Account credentials (hashed)
- Usage and activity logs
- Billing information (partial card details only)
- Any data submitted by Data Subjects via the Controller's application

## 5. Processor Obligations

The Processor shall:

a) Process personal data only on documented instructions from the Controller.  
b) Ensure persons authorised to process data are bound by confidentiality.  
c) Implement appropriate technical and organisational security measures (Article 32 GDPR).  
d) Respect conditions for engaging sub-processors (Article 28(2) GDPR). Current sub-processors are listed at `/api/v1/gdpr/sub-processors`.  
e) Assist the Controller in responding to Data Subject rights requests.  
f) Delete or return all personal data upon termination.  
g) Make available information necessary to demonstrate compliance.

## 6. Security Measures

The Processor implements:
- Encryption at rest (AES-256) and in transit (TLS 1.3)
- Tenant data isolation via Postgres Row-Level Security
- Envelope encryption for PII columns
- Role-based access controls
- Audit logging of all data access
- Annual penetration testing

## 7. Sub-Processors

The Controller authorises the Processor to engage sub-processors. A current list is maintained at `/api/v1/gdpr/sub-processors`. The Processor shall notify the Controller of any changes.

## 8. Data Subject Rights

The Processor shall assist the Controller with: access requests, rectification, erasure, restriction, portability, and objection — within the timeframes required by applicable law.

## 9. Data Breach Notification

The Processor shall notify the Controller without undue delay upon becoming aware of a personal data breach, providing information required under Article 33(3) GDPR.

## 10. Governing Law

This DPA is governed by the laws of the jurisdiction agreed in the main service agreement.

---
*Template version: 1.0 — customise before signature.*
