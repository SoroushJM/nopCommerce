# Project instructions

## Question tools

Ask all questions submitted through question tools (such as `request_user_input` and `request_user_input_async`) in English. Use English for question titles, headers, prompts, answer options, and option descriptions.

## Original nopCommerce source

Do not modify original nopCommerce code, views, styles, or project files except for Persian localization of the admin panel. Keep storefront behavior and presentation in custom plugins, Storefront libraries, and the Madadrang or MishaShop themes. Use custom build integration and `src/Storefront.sln` rather than changing native project files or `src/NopCommerce.sln`.

Run `python scripts/check-nopcommerce-core.py` when changing integration or native files to verify this boundary. Its admin exceptions require review for Persian localization scope.

Review changes to shared UI for effects on both themes. Keep each theme's visual styling independent and use Blazor Blueprint components wherever practical on pages being corrected.

## Feature preservation

Do not remove any nopCommerce feature when customizing, integrating, or fixing the storefront UI. Preserve existing capabilities and behavior, including sorting, page-size selection, grid/list views, filters, pagination, and their native data and event contracts. A styling fix must retain the feature and correct its presentation.
