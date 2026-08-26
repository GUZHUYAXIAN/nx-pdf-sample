# Private sample area

`private/` contains confidential or non-redistributable CAD validation inputs and generated reference material. The directory is intentionally ignored by Git.

Rules:

- Keep each drawing PRT and all required model dependencies in the same directory.
- Do not rename or edit the supplied samples.
- Do not commit private files or generated PDFs.
- Run destructive and missing-dependency tests only on temporary copies.
- Verify source SHA-256 before and after every live NX test.
