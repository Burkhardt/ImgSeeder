# ImgSeeder 3.13.0 Release Notes

## Summary

- Releases `ImgSeeder` version `3.13.0` with the `iorg` command.
- Carries forward `-rmc` as the short option alias for cache-delete mode while keeping `--rm-cache`.
- Keeps the aligned help output formatting and updates fallback package defaults to `JsonPit 3.13.0`, `OsLibCore 3.13.0`, `RaiUtils 3.13.0`, and `RaiImage 3.13.0`.
- Preserves the coordinated release order immediately before `PitSeeder`.
- No CLI behavior changes from `3.12.0`.

## Validation

- `dotnet test ImgSeeder.slnx --nologo -v minimal`
- Publication remains wired through the tag-triggered `publish-nuget.yml` workflow.
