# Security Policy

## Supported Versions

NStalling.Avro is pre-1.0. Security fixes are made against the latest published release only.

## Reporting a Vulnerability

Please report security vulnerabilities privately through [GitHub Security Advisories](https://github.com/NateIsStalling/NStalling.Avro/security/advisories/new) rather than filing a public issue.

Include:

- A description of the vulnerability and its potential impact
- Steps to reproduce, including any relevant schema/payload/configuration examples
- Affected version(s), if known

## Scope

NStalling.Avro resolves CLR types for Avro deserialization from a closed, explicitly configured allowlist. Type and version discriminator values read from payload data are never used directly to load a type; they only select an Avro schema, whose name is then resolved through that allowlist.

A path where untrusted input causes type resolution to bypass the allowlist is the primary concern here, but any vulnerability in NStalling.Avro-specific code is in scope for a report.
