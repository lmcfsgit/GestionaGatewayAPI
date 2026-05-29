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
