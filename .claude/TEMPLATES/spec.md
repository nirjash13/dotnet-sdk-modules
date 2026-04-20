# Feature Spec: [Feature Name]

## Problem
[What user need or business requirement does this address?]

## In Scope
- [What this feature WILL do]
- [Specific endpoints / handlers / EF changes]

## Out of Scope
- [What this feature will NOT do]

## Acceptance Criteria
- [ ] [Specific, testable criterion]
- [ ] [e.g., "GET /api/v1/jobs returns paginated results with total count"]
- [ ] [e.g., "Unauthenticated requests return 401"]

## API Contract (if applicable)
```
POST /api/v1/jobs
Authorization: Bearer <token>
Body: { "title": "string", "description": "string", "organizationId": "uuid" }
Response 201: { "id": "uuid", "title": "string", "status": "Draft", "createdAt": "datetime" }
Response 400: ProblemDetails with field errors
Response 401: ProblemDetails
```

## EF Core / DB Impact
- [ ] New migration required?
- [ ] New tables/columns: [list]
- [ ] Breaking schema change: yes/no

## Security Considerations
- Authentication required: yes / no (explain if no)
- Authorization: [which roles/policies]
- Input validation: [which fields, rules]

## Edge Cases
- [What happens when X is null/empty/missing?]
- [Concurrent modification scenario?]
- [Large dataset pagination?]

## Non-Goals
[Explicitly state what is NOT addressed by this spec]

<!-- written-by: writer-haiku | model: haiku -->
