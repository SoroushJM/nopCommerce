# Formatting the authored storefront files

The storefront uses Microsoft's C# and Razor formatting tools with the repository's `.editorconfig`. Two scoped `.editorconfig` files, in the storefront UI and PersianStorefront plugin directories, set `csharp_preserve_single_line_blocks` and `csharp_preserve_single_line_statements` to `false`. Upstream project rules are unchanged.

Luna ran SDK 10.0.401 `dotnet format whitespace` on the authored C# files. This expands compact method blocks. The scoped C# settings also set `csharp_style_expression_bodied_methods = false:suggestion`; Microsoft's `dotnet format style --diagnostics IDE0022 --severity info` converts expression-bodied methods into block bodies. Expression-bodied properties, lambdas and automatic properties retain the repository's existing rules.

Run the plugin formatter from its project directory with project-relative `--include` filenames. A root-relative include initially produced zero fixes; the corrected invocation applied IDE0022 to all six remaining plugin methods. Both projects passed style and whitespace verification afterward. The final scan found no compressed block-bodied functions; three trivial forwarding constructors retain their short form.

`StorefrontRoot.razor`, `Components/NativeHeader.razor` and `Components/NativeProductPage.razor` now have adjacent `.razor.cs` partial classes. Their final `@code` blocks were extracted verbatim, while `@inject` and `@implements` remain in the Razor files. This lets the C# formatter handle the methods without the Razor provider's unstable mixed-markup indentation.

Existing Razor-generated continuation lines in those three code-behind files were normalized through Microsoft's Roslyn `CSharpSyntaxTree.ParseText` and `NormalizeWhitespace` APIs in a private temporary tool, then passed through `dotnet format`. Original and normalized token kinds/text and comments were checked for equality; syntax diagnostics were empty. The temporary tool is not a repository dependency. The provider made no changes to the small `SfIcon.razor` parameter block and verified it as stable, so it was retained.

The Microsoft VS Code Razor provider (`ms-dotnettools.csharp`) formatted the stable Razor/CSHTML files. It failed its repeated-pass convergence check on the remaining mixed markup in `StorefrontRoot.razor` and the new plugin `Views/ProductCard.cshtml`; those files were restored to their pre-format bytes. Their markup was not manually reformatted. Workspace VS Code settings were restored unchanged.

The selected C# files passed `dotnet format whitespace --verify-no-changes`; the plugin and browser client subsequently built successfully. `git diff --check` was clean. Formatting was performed locally, without publishing or pushing.

## 2026-10-09 follow-up: phase 3 and tests

Microsoft `dotnet format style` (`IDE0022`) and `dotnet format whitespace` were run on the authored PersianStorefront plugin, Storefront.UI C# and Nop.Storefront.Tests C# files. The tests project now has a local `.editorconfig` with the same three scoped C# rules as the plugin and UI. The style pass expanded expression-bodied test methods (including `LocalAdminFixture.ShouldSkip`, `PersianAdminLocalizationTests.Read`, `NativeCategoryAdministrationTests.Fields`, `StorefrontClient` forwarding methods, and the `Selection` helper) and formatted phase 3 source. Compact residual `catch`/`finally` and inline block bodies in `AdminLanguage.cs`, `Infrastructure.cs`, `PersianStorefrontController.cs`, `StorefrontApiTests.cs`, and `NativeProductTests.cs` were normalized through the private Microsoft Roslyn `NormalizeWhitespace` helper. Token kinds/text and comments matched before and after; all five inputs had zero syntax errors before and after. No manual source formatting was used.

The plugin and tests style/whitespace verification passed. UI style verification passed; UI whitespace verification passed when excluding the pre-existing `Navigation.cs` auto-property block layout, which the scoped `csharp_preserve_single_line_blocks = false` rule would expand into a less readable multiline accessor form. `git diff --check` passed. TUnit and UI builds passed with zero warnings/errors. The plugin full build was blocked by the live Nop.Web DLL lock; its `Compile` target passed with `BuildProjectReferences=false`, avoiding host copy operations. No build was run by the formatting agent.

## 2026-10-09 cart component follow-up

`CartItem.razor` and `ProductCard.razor` had embedded `@code` method bodies. Their final blocks were moved verbatim into adjacent `CartItem.razor.cs` and `ProductCard.razor.cs` partial classes, leaving component markup unchanged. Microsoft `dotnet format style` IDE0022 converted `SetQuantity` and `Money` to block-bodied methods, and `dotnet format whitespace` was applied. Because whitespace formatting preserved malformed continuation breaks in the extracted CartItem members, the private Roslyn `NormalizeWhitespace` helper was used on those two code-behind files; token kinds/text and comments were unchanged, with zero syntax errors. Targeted IDE0022/import and whitespace verification passed, as did `git diff --check`.

The formatter left the three empty forwarding record constructors in `Contracts.cs` on one line. `SfIcon.razor` contains only two short auto-properties; the previously tested Microsoft Razor provider was a stable no-op there. No JS formatter is configured in the storefront package (its only scripts build/watch Tailwind CSS); storefront JavaScript functions were already multiline. Inline Razor event lambdas in the root/header components were left unchanged.

The root agent subsequently built `Storefront.UI` with zero warnings and errors.

## 2026-10-09 Preview compact-function follow-up

Luna audited authored storefront, plugin, and TUnit source for remaining compressed function bodies. The remaining substantive cases were `Storefront.Preview/PreviewStore.cs` and the startup/lambda blocks in `Storefront.Preview/Program.cs`. Microsoft `dotnet format whitespace` expanded the preview methods, and the existing private Microsoft Roslyn `NormalizeWhitespace` helper expanded the nested session declarations and startup blocks that the whitespace pass preserved. No manual source formatting or behavioral refactoring was performed. The temporary Preview `.editorconfig` used during inspection was removed; repository settings remain authoritative.

Roslyn confirmed identical token kinds/text and comments for `PreviewStore.cs` (1,321 tokens, zero comments) and `Program.cs` (475 tokens, zero comments), with no syntax errors before or after. Targeted whitespace verification passed for `PreviewStore.cs`. For `Program.cs`, verification reports conflicting end-of-file changes (`WHITESPACE` requests a CRLF while `FINALNEWLINE` requests its deletion under `insert_final_newline = false`); the applied formatter leaves the file without a final newline. This remaining diagnostic concerns the file ending, not the expanded function bodies.

`dotnet build Storefront.Preview.csproj --no-restore` succeeded with zero warnings and errors, and `git diff --check` passed. The remaining audit matches were empty forwarding constructors, auto-properties, and previously retained inline Razor event lambdas. No compact block-bodied C# functions were found in the audited authored source. The formatting changes were committed in `8f80f8c57f`.

## 2026-10-09 Razor callback follow-up

The compact three-statement cart reset callbacks in `StorefrontRoot.razor` and `Components/NativeHeader.razor` are now multiline. A temporary Microsoft Roslyn helper parsed and normalized only the two exact C# lambda expressions. Each lambda retained the same 21 token kinds/text values and zero comments, with zero syntax errors before and after. No handler logic or surrounding markup was rewritten.

`dotnet build src/Storefront/Storefront.UI/Storefront.UI.csproj --no-restore` passed with zero warnings and errors, confirming the multiline quoted Razor attributes compile. The project build ran its existing Tailwind build script and rewrote the already-modified generated `wwwroot/storefront.css`. `git diff --check` passed.
