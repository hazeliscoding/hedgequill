# Security

## Reporting a vulnerability

Report it privately: on this repository's **Security** tab, choose **Report a vulnerability**. Please don't open a public issue for it. You'll get a reply there.

## Supported versions

The latest release. Hedgequill updates itself, so a fix reaches everyone through the next one.

## Worth reporting

- **The update check,** the app's only network request. It asks GitHub Releases for a newer version through [Velopack](https://velopack.io) and installs it when Hedgequill closes. Anything that makes it fetch from somewhere else, send something about the writer, or install a package that isn't the project's is a vulnerability.
- **Opening a project.** A project is a folder of Markdown files and a `hedgequill.json`. A folder crafted so that Hedgequill reads, writes or deletes outside it, or loses text it was given, is a vulnerability.
- **The Windows installer's signature.** Setup and `Hedgequill.exe` are signed by Hazel Granados through Azure Artifact Signing; a release that isn't is worth reporting. The Mac builds are signed ad hoc and not notarized, as the README explains.
