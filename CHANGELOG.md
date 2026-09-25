# Changelog

All notable changes to Gestiona Gateway API are documented in this file.

## [1.9.2]

### Added

- Support for various response body encoding: UTF8, Windows-1252. The request should set the header Accept-Charset: windows-1252

### Changed

### Fixed

- For paginated content aggregates and returns the aggregated content. Follows the links array where rel="next" to get all the pages from the Gestiona response paginated content.

## [1.9.1]

### Added

### Changed

### Fixed

- Endpoints don't perform automatic model binding using [FromBody]. The binding is done manually: read the body and then deserialize.

### [1.9.0]

#### Added

- Added `POST /addon/authorizations` to create an authorization.
- Added `GET /addon/authorizations/{authId}` to verify authorization state.

### [1.8.1]

#### Added

- Added `GET /processes/{process_id}/documents/{document_id}/signatures` to get
  document signature information.

### [1.8.0]

#### Added

- Added `POST /processes/related` to create a relation between processes.
- Added `DELETE /processes/{process_id}/related/{related_process_id}` to delete a
  relation between processes.
- Added `GET /processes/{process_id}/related` to get related processes.

### [1.7.0]

#### Added

- Added `GET /queues/connectors/{connector_name}/{message_id}` to get signed
  document or message information.
- Added `POST /queues/connectors/{connector_name}/{message_id}` to process a
  message.
- Added `GET /queues/connectors/{connector_name}` to get messages for a connector.

### [1.6.2]

#### Changed

- `GET /processes/assignees/users` now accepts `username` as a query parameter.
  The query parameter takes precedence over the JSON body.

### [1.6.1]

#### Changed

- Added `activityId` to each procedure returned by the activity procedures
  endpoint.

### [1.6.0]

#### Added

- Added `GET /processes/assignees/users` to get an assignable user.
- Added `GET /processes/assignees/groups` to get assignable groups.
- Added `GET /activities` to get all activities.
- Added `GET /activities/{activity_id}/procedures` to get the procedures for an
  activity.

#### Changed

- When a newly created process has selectable titles, the first title is sent in
  the open-file request as `selectable_title`.

### [1.5.0]

#### Added

- Added `POST /processes` to create a Gestiona process.

### [1.4.0]

#### Added

- Added `GET /processes/{process_id}/documents` to retrieve all documents from a
  process.
- Added `GET /processes/{process_id}/documents/{document_id}` to retrieve the
  documents from a folder.

### [1.3.2]

#### Fixed

- Ignored an unresolved Postman `X-User-Access-Token` variable whose literal
  value was `{{X-User-Access-Token}}`.

#### Changed

- Added masked authentication-token logging in Debug mode.

### [1.3.1]

#### Added

- Added `GET /processes?process_number={process_number}` to resolve a process ID
  from a process number.

#### Changed

- Added debug logging for request bodies sent to the Gestiona API.
- Read the API version from the project file instead of `appsettings.json`.
- Added zone (`Concelho`) to third-party addresses.
- Added parish code (`Freguesia`), obtained from the last path segment of the
  link whose `ref` is `parish`.
- Added Gestiona's `second_surname` field to the third-party model.

### [1.2.1]

#### Added

- Added support for the `X-User-Access-Token` header. When present, its value is
  used as `X-Gestiona-Access-Token`; otherwise, `Gestiona__AccessToken` is used.
- Added Swagger UI at `/swagger` in the Development environment.

### [1.1.1]

#### Added

- Added middleware that logs request client information.
- Added `GET /processes/{process_id}/thirds`.
- Added `GET /processes/thirds?process_number={process_number}`.
- Added `GET /thirds/{third_id}`.
- Added `GET /thirds?nif={nif}`.

#### Changed

- Request-body logging no longer writes document content; it only records that
  content is present.
