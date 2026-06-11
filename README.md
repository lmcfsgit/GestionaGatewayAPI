# Gestiona Gateway API

<center>_Version 1.1.1_</center>

### Features

- Middleware for logging request client information
- New endpoints:
  - GET .../processes/{process_id}/thirds
  - GET .../processes/thirds?process_number=<proces_number value>
  - GET .../thirds/{third_id}
  - GET .../thirds?nif=<nif>

### Bug Fixs

### Improvements

- The log of the request body doesn't print the Content anymore, it only informs that it's present.

<center>_Version 1.2.1_</center>

### Features

- Support for X-User-Access-Token header.
  If it's present uses this token for the X-Gestiona-Access-Token else use the environment variable Gestiona\_\_AccessToken

### Bug Fixs

### Improvements

- Added swagger support to the API: .../swagger
