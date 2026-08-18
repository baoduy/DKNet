# Operation Log

- **2026-06-21** — Initial wiki generated from the DKNet knowledge graph and `docs/`.
  Created 25 articles plus `index.md`, `log.md`, and `CLAUDE.md` covering
  architecture, core utilities, EF Core persistence, ASP.NET Core, service adapters,
  infrastructure/messaging, and testing.
- **2026-08-04** — Updated `audit-logs.md` for SEC-005: documented default sensitive-property
  redaction, the `[AuditLog]` per-property override, and the `AuditPropertyPolicy.OnlyAttributedProperties`
  strict mode.
- **2026-08-18** — Updated `dto-generator.md` and `domain-events.md` for DRK-437: documented the
  repeatable `[GenerateEvent]` attribute (naming, `Kinds`, update narrowing) and how
  `DKNet.EfCore.Events` raises declared events automatically after SaveChanges, including the
  `IMapper` mapping requirement and the nested-owned-value limitation. Refreshed both entries in
  `index.md`.
- **2026-08-18** — Reworked `dto-generator.md` and `domain-events.md` for DRK-450: replaced the
  single-annotation `[GenerateEvent]` shape with the two-declaration shape — a `[GenerateDto]`
  payload record plus a repeatable `RaisesEventAttribute` raise rule on the entity naming the
  payload, operations, and optional narrowing. The DtoGenerator now only validates raise rules
  (payload/entity match, narrowing property names) and emits no code for them. Refreshed both
  entries in `index.md`.
