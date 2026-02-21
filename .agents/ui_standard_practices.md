# UI Design Standards and Practices

This document outlines the standard practices and guidelines for producing modern, clean, and high-quality User Interfaces (UI) in this application. These rules must be followed when generating or modifying any UI components.

## 1. Overall Theme and Hierarchy
- **Modern & Clean**: Prioritize a clean, uncluttered layout. Use subtle shades of color to establish visual hierarchy instead of harsh lines.
- **Background Colors (Light Theme Example)**:
  - **Left Navigation Panel**: Should typically be the darkest area (`#D9DDE6` or similar) to ground the application.
  - **Main Content Area**: Should be slightly lighter than the side panel (`#ebecf0`) to draw focus.
  - **List/Action Panels**: Should use an intermediate or contrasting shade (`#DDE1E8`) if it contains selectable items, to ensure highlights are clearly visible.

## 2. Interactive Elements (Buttons, List Items)
All interactive elements must provide clear visual feedback for their different states. Avoid heavy primary colors (like default bright blues) unless explicitly requested; use subtle grey/white or dark grey/black tints instead.

### State Colors (Light Theme Example)
- **Normal (Default)**: `Transparent` or the base panel color.
- **Hover**: Slightly lighter or darker than the background (e.g., `#E2E5E8` or `#F2F5F8`). It should be a noticeable but soft shift.
- **Pressed (Click)**: More distinct than hover, often matching the selected state.
- **Selected (Active)**: Should be highly visible. For a light theme, use bright white (`#FFFFFF`) with a font weight increase (e.g., `SemiBold`) to clearly indicate the active item.

### Corner Radius
- Use rounded corners for buttons, cards, and list items. A `CornerRadius` of `6` to `8` is generally preferred for a softer, modern look.

## 3. Spacing and Geometry
- **Padding**: Elements should never feel cramped. Use generous padding, especially on the left/right of text inside buttons and list items (e.g., `Padding="10,6"` instead of `Padding="0"` or very small values).
- **Margins**: Ensure consistent spacing between neighboring elements (e.g., `Margin="8,1,8,1"` for list items).

## 4. Separators and Borders
- **Avoid Hard Lines**: Do not use hard 1px border lines (like dark grey or black lines) to separate major layout sections (such as grids and splitters).
- **Color Blocks**: Separate sections (e.g., left panel, top header, main content) by using contrasting background color blocks instead of borders.
- If borders are strictly necessary, make them very light and subtle.

## 5. Shadows and Depth
- Use DropShadowEffects sparingly and specifically to indicate floating elements or high-elevation components:
  - **Popups and Context Menus**: Should have soft, diffuse shadows (e.g., `BlurRadius=15`, `Opacity=0.15`).
  - **Cards**: Can have subtle shadows to stand out from the background.

## 6. Icons and Glyphs
- Ensure icons have appropriate contrast. Treat icon colors the same way you treat text—dark grey/black for light themes, and white/light grey for dark themes.
- If using vector shapes or masks, apply a subtle tint that shifts on hover, similar to the background color shift.

By adhering to these rules, the AI will ensure the UI remains consistent, premium-looking, and user-friendly.
