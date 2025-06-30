# DKNet Project Structure Checklist

Use this checklist to keep the project structure, documentation, and naming conventions consistent and clear as your repository evolves.

---

## 1. Top-Level Project Summary

- [ ] Main `README.md` includes a table or summary of all DKNET-prefixed projects.
- [ ] Each entry includes:
    - [ ] Project name
    - [ ] Short description
    - [ ] Link to source code
    - [ ] Link to documentation
- [ ] Table is updated whenever a project is added, removed, or renamed.

---

## 2. Folder and File Naming Consistency

- [ ] All source project folders use PascalCase (e.g., `DKNet.EfCore.Repos`).
- [ ] All documentation files use the chosen consistent naming convention (e.g., PascalCase: `DKNet.EfCore.Repos.md` or kebab-case: `dknet-efcore-repos.md`).
- [ ] Documentation folder structure matches source folder structure (e.g., `docs/EfCore/DKNet.EfCore.Repos.md` for `EfCore/DKNet.EfCore.Repos/`).
- [ ] All internal links in docs and README are updated after renaming or moving files.

---

## 3. Templates and Samples Organization

- [ ] All template/sample projects are located in `/templates` or `/samples` directory at the root.
- [ ] The main project summary table clearly indicates which entries are templates or samples.
- [ ] Documentation for templates/samples is stored under `docs/templates` or `docs/samples`.

---

## 4. Documentation Practices

- [ ] Each DKNET-prefixed project has a corresponding documentation file in `/docs`.
- [ ] All documentation follows the same structure and style guidelines.
- [ ] Quickstart or "How to use" sections are included for each project.
- [ ] Main `docs/README.md` lists all documentation files and their purposes.

---

## 5. General Maintenance

- [ ] Periodically review for unused, outdated, or duplicated files/folders.
- [ ] All project names, folder names, and documentation references are updated if a project is renamed or structure changes.
- [ ] New contributors are pointed to this checklist and guidelines in CONTRIBUTING.md.
