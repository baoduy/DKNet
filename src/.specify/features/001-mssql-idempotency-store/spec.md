# Feature Specification: MS SQL Storage for Idempotency Keys

**Feature Branch**: `001-mssql-idempotency-store`  
**Created**: January 30, 2026  
**Status**: Draft  
**Input**: User description: "Implement the Ms Sql storage for Idempotent key using Entity framework"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Store Idempotency Keys in MS SQL Database (Priority: P1)

As a developer using DKNet.AspCore.Idempotency, I want to persist idempotency keys and their cached responses in a MS SQL Server database using Entity Framework Core so that idempotent request replay is reliable across application restarts and distributed environments.

**Why this priority**: This is the core feature requirement. Without persistent database storage, idempotency keys are lost on application restart, defeating the purpose of idempotency in production systems. This enables reliability in distributed systems where requests may be replayed across instances.

**Independent Test**: Can be fully tested by creating a request with an idempotency key, storing it in the database, querying it back, and verifying the response is identical on replay. Delivers immediate value: persistent, reliable idempotency.

**Acceptance Scenarios**:

1. **Given** a new idempotency key, **When** a request is processed, **Then** the key and cached response are stored in the MS SQL database
2. **Given** an idempotency key that was previously processed, **When** a request with the same key arrives, **Then** the system retrieves the cached response from the database and returns it
3. **Given** an idempotency key with an expiration time, **When** the expiration time passes, **Then** the database entry is marked as expired and a new request with the same key is processed again
4. **Given** a database connection issue, **When** an idempotency key is checked, **Then** the system handles the error gracefully (fail-open or fail-closed based on configuration)

---

### User Story 2 - Query and Manage Idempotency Keys (Priority: P2)

As a developer, I want to query, filter, and manage idempotency keys stored in the database so that I can monitor, debug, and maintain idempotency state in production systems.

**Why this priority**: Operability and debugging. Database storage enables inspection of idempotent requests, filtering by route/key/expiration status, and manual cleanup of stale entries.

**Independent Test**: Can be fully tested by querying the IdempotencyKey table with various filters (route, status, creation date) and verifying correct results.

**Acceptance Scenarios**:

1. **Given** stored idempotency keys in the database, **When** I query with filters (route, status, creation date range), **Then** the system returns matching records with pagination support
2. **Given** an expired idempotency key, **When** I run a cleanup operation, **Then** expired entries are deleted from the database
3. **Given** an active idempotency key, **When** I query the database, **Then** I can see the route, key, cached status code, creation date, and expiration date

---

### User Story 3 - Configure MS SQL Storage Backend (Priority: P2)

As a developer, I want to configure DKNet.AspCore.Idempotency to use the MS SQL storage backend instead of distributed cache so that I can choose the appropriate storage strategy for my application.

**Why this priority**: Configuration and flexibility. Users should be able to opt-in to SQL storage while maintaining backward compatibility with distributed cache store.

**Independent Test**: Can be fully tested by configuring the service with SQL store and verifying requests are processed/cached correctly.

**Acceptance Scenarios**:

1. **Given** a configured ASP.NET Core application, **When** I register the MS SQL idempotency store via `AddIdempotencyMsSqlStore()`, **Then** the application uses database storage instead of cache
2. **Given** an application configured with MS SQL storage, **When** I query the database, **Then** all idempotency keys and responses are persisted correctly
3. **Given** database schema setup, **When** migrations are applied, **Then** the IdempotencyKey table is created with proper indexes and constraints

---

### User Story 4 - Concurrent Request Handling with Database Locking (Priority: P3)

As a system architect, I want MS SQL storage to safely handle concurrent duplicate requests so that the first request wins and subsequent duplicates within the same logical transaction use the cached response without race conditions.

**Why this priority**: Production safety. Concurrent requests with the same idempotency key should be handled atomically using database constraints and transactions.

**Independent Test**: Can be fully tested by sending concurrent requests with the same idempotency key and verifying only one is processed and subsequent ones receive the cached response.

**Acceptance Scenarios**:

1. **Given** two concurrent requests with identical idempotency keys, **When** both arrive within the same timeframe, **Then** the first one is processed and the second receives the cached response from the first
2. **Given** a race condition scenario, **When** two requests try to insert the same idempotency key simultaneously, **Then** the database constraint prevents duplicate entries and returns the existing one

---

### Edge Cases

- What happens when the database is unavailable during request processing?
- How does the system handle NULL or empty idempotency keys?
- What occurs when an idempotency key is reused after expiration?
- How does the system handle keys with special characters or extremely long values?
- What happens when the cached response body is larger than database limits?
- How does the system behave with transactions that span multiple requests?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support storing idempotency keys and cached responses in MS SQL Server database
- **FR-002**: System MUST retrieve previously processed requests from the database and return cached responses
- **FR-003**: System MUST respect expiration times for idempotency keys stored in the database
- **FR-004**: System MUST handle concurrent requests with the same idempotency key using database-level synchronization
- **FR-005**: System MUST provide Entity Framework Core DbContext for IdempotencyKey table
- **FR-006**: System MUST support automatic database migrations for the IdempotencyKey schema
- **FR-007**: System MUST validate and sanitize idempotency keys before database storage
- **FR-008**: System MUST provide queryable interface for inspecting stored idempotency keys
- **FR-009**: System MUST support configuration via dependency injection (AddIdempotencyMsSqlStore)
- **FR-010**: System MUST log all database operations (queries, inserts, updates) with appropriate log levels
- **FR-011**: System MUST handle database errors gracefully with configurable fail-open or fail-closed behavior
- **FR-012**: System MUST support cleanup/expiration of old idempotency keys from the database
- **FR-013**: System MUST maintain backward compatibility with existing distributed cache store

### Key Entities *(include if feature involves data)*

- **IdempotencyKey**: Represents a stored idempotency key with its cached response
  - `Id` (Guid): Primary key
  - `Key` (string): The actual idempotency key value
  - `Route` (string): The API route that was called
  - `HttpMethod` (string): The HTTP method (GET, POST, etc.)
  - `CachedResponse` (JSON): The serialized HTTP response (status code, body, content-type)
  - `StatusCode` (int): HTTP status code of the cached response
  - `CreatedAt` (DateTime): When this idempotency key was first processed
  - `ExpiresAt` (DateTime): When this idempotency key expires
  - `IsProcessed` (bool): Whether the original request completed successfully
  - `ProcessingCompleted` (DateTime?): When the original request completed

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All idempotency keys are persisted in MS SQL database with 100% reliability
- **SC-002**: Request replay using database-stored keys completes with <50ms latency (vs <10ms for cache)
- **SC-003**: Concurrent duplicate requests are handled safely with zero race conditions (verified by concurrent request tests)
- **SC-004**: Database schema can be created/migrated with EF Core migrations (dotnet ef migrations add IdempotencyKeySchema)
- **SC-005**: Configuration is intuitive and integrates seamlessly with existing ASP.NET Core DI (one line: `services.AddIdempotencyMsSqlStore(config)`)
- **SC-006**: Test coverage for MS SQL store is ≥85% (measured via code coverage tools)
- **SC-007**: All public APIs have XML documentation with examples
- **SC-008**: Zero compiler warnings (`TreatWarningsAsErrors=true`)
- **SC-009**: All existing idempotency tests pass without modification (backward compatible)
- **SC-010**: Performance testing shows <5% degradation vs distributed cache for read operations on keys <10KB
