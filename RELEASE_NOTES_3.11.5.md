# ImgSeeder 3.11.5 Release Notes

## Summary

- Releases `ImgSeeder` version `3.11.5` with the `iorg` command.
- Carries forward `-rmc` as the short option alias for cache-delete mode while keeping `--rm-cache`.
- Keeps the aligned help output formatting and updates fallback package defaults to `JsonPit 3.11.5`, `OsLibCore 3.11.5`, `RaiUtils 3.11.5`, and `RaiImage 3.11.5`.
- Preserves the coordinated release order immediately before `PitSeeder`.
- No CLI behavior changes from `3.11.4`.

## Validation

- `dotnet test ImgSeeder.slnx --nologo -v minimal`
- `dotnet run --project ImgSeeder.csproj -- -h -c OneDrive -r LiveAfricaStageImage`
- Publication remains wired through the tag-triggered `publish-nuget.yml` workflow.
