# Formatting the authored storefront files

The storefront uses Microsoft's C# and Razor formatting tools with the repository's `.editorconfig`. Two scoped `.editorconfig` files, in the storefront UI and PersianStorefront plugin directories, set `csharp_preserve_single_line_blocks` and `csharp_preserve_single_line_statements` to `false`. Upstream project rules are unchanged.

Luna ran SDK 10.0.401 `dotnet format whitespace` on the authored C# files. This expands compact method blocks; expression-bodied members and automatic properties can remain short under Microsoft's rules.

`StorefrontRoot.razor` and `Components/NativeHeader.razor` now have adjacent `.razor.cs` partial classes. Their final `@code` blocks were extracted verbatim, while `@inject` and `@implements` remain in the Razor files. This lets the C# formatter handle the methods without the Razor provider's unstable mixed-markup indentation.

Existing Razor-generated continuation lines in those two code-behind files were normalized through Microsoft's Roslyn `CSharpSyntaxTree.ParseText` and `NormalizeWhitespace` APIs in a private temporary tool, then passed through `dotnet format`. Original and normalized token kinds/text and comments were checked for equality; syntax diagnostics were empty. The temporary tool is not a repository dependency.

The Microsoft VS Code Razor provider (`ms-dotnettools.csharp`) formatted the stable Razor/CSHTML files. It failed its repeated-pass convergence check on the remaining mixed markup in `StorefrontRoot.razor` and the new plugin `Views/ProductCard.cshtml`; those files were restored to their pre-format bytes. Their markup was not manually reformatted. Workspace VS Code settings were restored unchanged.

The selected C# files passed `dotnet format whitespace --verify-no-changes`; the plugin and browser client subsequently built successfully. `git diff --check` was clean. Formatting was performed locally, without publishing or pushing.
