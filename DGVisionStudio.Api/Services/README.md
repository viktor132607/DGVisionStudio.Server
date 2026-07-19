# Service structure

Services are grouped by business domain. Public controller-facing interfaces remain stable; larger implementations are exposed through small facade services.

- `Auth/` — registration, sessions, passwords, account and privacy operations.
- `Commerce/` — pricing and print-request workflows.
- `Communication/` — contact requests, email and calendar/reminder workflows.
- `Galleries/` — admin gallery orchestration, access, archives and media management.
- `ClientGalleries/` — client-gallery core, admin, access, photo, user and lifecycle workflows.
- `Media/Slideshow/` — slideshow images, settings and intro-video handling.
- `Portfolio/` — portfolio category, album, image and public-query services.
- `Storage/` — local and cloud file-storage implementations.
- `System/` — health, statistics, users, settings and small system endpoints.
- `Shared/` — cross-domain result and auditing infrastructure.
- `Interfaces/` — controller-facing service contracts.

Facade services keep existing interfaces and delegate work to smaller query, command, upload, download or lifecycle services.
