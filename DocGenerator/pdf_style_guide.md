# Technical PDF Generation Style Guide

## General Layout
- Use a clean technical-documentation style.
- Optimize vertical spacing to reduce unnecessary whitespace while keeping the document easily readable.
- Use compact but readable line heights.
- Use consistent margins:
  - Left/right: compact
  - Top/bottom: moderate

## Typography

### Main Title (`#`)
- Large font
- Center aligned
- Dark blue color
- Extra spacing after title

### First-Level Sections (`##`)
- Each first-level section must start on a new page.
- Use:
  - white text
  - colored background (consistent color for all same-level sections)
  - padding around text

### Second-Level Sections (`###`)
- Use a different consistent color for all same-level headings.
- Slight spacing before/after heading

### Body Text
- Compact line height
- Small but readable font
- Minimal spacing between paragraphs

## Code / JSON Blocks

### Styling
- Light gray background
- Monospace font
- Thin border
- Internal padding

### Pagination Rules
- JSON/code blocks must never split awkwardly across pages.
- If the block does not fit:
  - move the entire block to the next page.

### Compactness
- Use reduced line height for code blocks.
- Keep blocks readable but space-efficient.

## Lists / Bullets

### Bullet Formatting
- Bullets must be indented.
- Use proper hierarchical spacing.
- Maintain compact vertical spacing.

## Page Structure

### Section Pagination
- Every first-level section (`##`) starts on a new page.

### Flow
- Avoid isolated headings at page bottoms.
- Keep headings close to their related content.

## Visual Consistency

### Color Rules
- Same heading level = same color/style.
- Maintain a professional API documentation appearance.

Suggested palette:
- Title: dark blue
- `##`: blue background + white text
- `###`: orange
- Code blocks: light gray

## Readability Rules

- Compact layout preferred over excessive whitespace.
- Never sacrifice readability for compactness.
- Maintain:
  - sufficient padding
  - good contrast
  - readable font sizes
  - clear hierarchy

## Content Preservation

- Preserve all provided content exactly.
- Never truncate sections.
- Never omit JSON examples.
- Maintain original ordering and structure.
