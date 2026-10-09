# Formatting the authored storefront files

The storefront uses Microsoft's C# and Razor formatting tools with the repository's `.editorconfig`. Two scoped `.editorconfig` files, in the storefront UI and PersianStorefront plugin directories, set `csharp_preserve_single_line_blocks` and `csharp_preserve_single_line_statements` to `false`. Upstream project rules are unchanged.

Luna ran SDK 10.0.401 `dotnet format whitespace` on the authored C# files. This expands compact method blocks. The scoped C# settings also set `csharp_style_expression_bodied_methods = false:suggestion`; Microsoft's `dotnet format style --diagnostics IDE0022 --severity info` converts expression-bodied methods into block bodies. Expression-bodied properties, lambdas and automatic properties retain the repository's existing rules.

Run the plugin formatter from its project directory with project-relative `--include` filenames. A root-relative include initially produced zero fixes; the corrected invocation applied IDE0022 to all six remaining plugin methods. Both projects passed style and whitespace verification afterward. The final scan found no compressed block-bodied functions; three trivial forwarding constructors retain their short form.

`StorefrontRoot.razor`, `Components/NativeHeader.razor` and `Components/NativeProductPage.razor` now have adjacent `.razor.cs` partial classes. Their final `@code` blocks were extracted verbatim, while `@inject` and `@implements` remain in the Razor files. This lets the C# formatter handle the methods without the Razor provider's unstable mixed-markup indentation.

Existing Razor-generated continuation lines in those three code-behind files were normalized through Microsoft's Roslyn `CSharpSyntaxTree.ParseText` and `NormalizeWhitespace` APIs in a private temporary tool, then passed through `dotnet format`. Original and normalized token kinds/text and comments were checked for equality; syntax diagnostics were empty. The temporary tool is not a repository dependency. The provider made no changes to the small `SfIcon.razor` parameter block and verified it as stable, so it was retained.

The Microsoft VS Code Razor provider (`ms-dotnettools.csharp`) formatted the stable Razor/CSHTML files. It failed its repeated-pass convergence check on the remaining mixed markup in `StorefrontRoot.razor` and the new plugin `Views/ProductCard.cshtml`; those files were restored to their pre-format bytes. Their markup was not manually reformatted. Workspace VS Code settings were restored unchanged.

The selected C# files passed `dotnet format whitespace --verify-no-changes`; the plugin and browser client subsequently built successfully. `git diff --check` was clean. Formatting was performed locally, without publishing or pushing.
