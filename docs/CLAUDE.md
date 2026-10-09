# Docs (Obsidian vault)

`docs/` is an Obsidian vault. It holds the project's requirements and code documentation.

## Structure

- `Requirements/`: product and functional requirements. This is the source of truth for what the system must do.
- `Code/`: technical documentation for `api` and `ui`. Add notes here as code appears.
  - `Code/Design/`: use case diagram, UML class diagrams, architecture with sequence diagrams, and design patterns (GoF, GRASP, SOLID).
  - `Code/Specs/` and `Code/Plans/`: design specs and implementation plans per feature.

## Conventions

- Write notes in Obsidian-flavored Markdown. Use `[[wikilinks]]` to link between notes.
- Diagrams are Mermaid code blocks (Obsidian and GitHub render them). Draw them from real class, method and endpoint names, and update them when the code changes.
- Don't edit `.obsidian/`. It holds the user's vault settings.
- This CLAUDE.md file shows up as a note inside the vault. That's expected.
