# Origin — nature behind intelligence

## Overview

An editorial, reassuring approach to sophisticated software: luminous natural scenery and delicate serif headlines surround dark, finely detailed interfaces. Blue sky, leafy green and warm sand punctuate a predominantly near-black page, with white actions and small monospaced markings. The feeling is spacious and personal rather than technical or institutional, suited to products that turn complex information into clear next steps.

### Essentials

- Headlines feel spoken rather than engineered: light Lyon Display serif at 96px/1.15 in the opening and 80px/1 in chapters, with a single meaningful word or phrase in matching italic.
- The opening is an atmosphere with a product inside it: full-bleed blue sky behind centered copy in an 830px wrapper, a 420px support line and a 630px-wide phone rising from below the action.
- Actions are small white instruments against expansive scenery: 48px high, 8px corners, 12px uppercase Roboto Mono and a small right arrow; primary actions are not pills.
- Color belongs to scenes, not the whole interface: near-black #050505 dominates the long middle, while nature panels and restrained blue-violet light supply concentrated color. Ordinary cards use 4% white fill and 8% white edges.
- Product proof floats in darkness: 24px rounded cards, inset miniature interfaces and widely separated details, paired with edge-to-edge nature imagery. Include this image/interface contrast; if no device exists, stage a legible outcome card instead.
- Atmosphere returns at the finish: a monumental 96px serif conversion line with italic action word sits on image-backed scenery fading into the dark footer. Include a soft blue-violet light seam elsewhere as the abstract counterpart to the photography.

## Colors

### Accent

| Name | Value | Token | Roles | Use |
| --- | --- | --- | --- | --- |
| Light blue | `#219ff8` | `--color-blue` | accent | Blue end of the atmospheric light seam. |
| Golden atmosphere | `#cc7c01` | `--color-ochre` | background | Blurred lower glow behind warm flower imagery. |
| Atmospheric violet | `#847dff` | `--color-violet` | accent, background | Light seams and faint corner illumination, not ordinary text or primary buttons. |
| Dune atmosphere | `#a7612e` | `--color-earth` | background | Blurred lower glow behind sand imagery. |
| Meadow atmosphere | `#61a133` | `--color-green` | background | Blurred lower glow behind meadow imagery. |

### Neutral

| Name | Value | Token | Roles | Use |
| --- | --- | --- | --- | --- |
| White | `#ffffff` | `--color-white` | text, action | Headlines, primary actions and bright interface details. |
| Action ink | `#000000` | `--color-black` | text | Text on white actions. |
| Near-black canvas | `#050505` | `--color-canvas` | background | Default long-form page canvas. |
| Translucent card | `#0f0f0f` | `--color-card` | surface | Visible 4% white fill over the default canvas. |
| Graphite dark | `#131313` | `--color-panel-dark` | surface | Dark end of charcoal panels and footer input gradient. |
| Card hairline | `#191919` | `--color-edge` | border, surface | Visible 8% white edges and small controls over the default canvas. |
| Control graphite | `#222222` | `--color-control-top` | surface | Upper end of footer input and icon-button gradients. |
| Divider gray | `#282828` | `--color-rule` | border | 14% white separator over the default canvas. |
| Muted gray | `#555555` | `--color-muted` | text | 32% white over the default canvas; card descriptions, timestamps and attribution. |
| Supporting gray | `#9b9b9b` | `--color-support` | text | 60% white over the default canvas; supporting paragraphs and footer links. |
| Soft white | `#fafafa` | `--color-heading` | text | Chapter titles and selected support headings. |

### Page-type variants

| Only for | Name | Value | Token | Roles | Use |
| --- | --- | --- | --- | --- | --- |
| focused feature demonstrations | Cyan field | `#00b3dd` | `--color-cyan` | background | Oversized rounded shape within sculptural panels. |
| focused feature demonstrations | Purple field | `#4b49aa` | `--color-purple-base` | background | Backing for sculptural multicolor panels. |
| focused feature demonstrations | Orchid field | `#dd90d8` | `--color-pink` | background | Tall rounded shape within sculptural panels. |
| focused feature demonstrations | Lilac field | `#d1c9ff` | `--color-lilac` | background | Broad rounded shape within sculptural panels. |
| institutional, business and focused feature pages | Charcoal canvas | `#0f1011` | `--color-secondary-canvas` | background | Alternate page canvas and the destination of image fades. |
| business, institutional and focused feature cards | Graphite light | `#2b2b2c` | `--color-panel-top` | surface | Upper-left end of diagonal charcoal panels. |
| business, institutional and focused feature cards | Graphite base | `#2e2e2e` | `--color-panel-base` | surface | Backing beneath charcoal gradient panels. |

## Typography

- **Lyondisplay App** — Opening, chapter, card and closing serif titles. Fine, high-contrast editorial serif with a flowing true italic; predominantly light weight. If unavailable, use **Cormorant Garamond**. Use 300 and true italic; tune wrapping to preserve the broad editorial lines. Font stack: "Lyondisplay App", "Cormorant Garamond", Georgia, serif.
- **Lyondisplay** — Compact editorial promise headings. Related light editorial serif with matching italic. If unavailable, use **Cormorant Garamond**. Font stack: "Lyondisplay", "Cormorant Garamond", Georgia, serif.
- **Inter** — Default body copy, feature titles and testimonials. Neutral, light grotesk that recedes behind the serif. Font stack: "Inter", Arial, sans-serif.
- **Suisseintl** — Footer and alternate page body copy. Light neo-grotesk with straightforward compact proportions. If unavailable, use **Inter**. Font stack: "Suisseintl", "Inter", Arial, sans-serif.
- **Roboto Mono** — Navigation, actions and small category markings. Compact mechanical monospace contrasting with expressive serif. Font stack: "Roboto Mono", monospace.
- **Suisseintltrial** (only for business proof sections) — Business proof numerals. Regular neo-grotesk, keeping numbers unembellished. If unavailable, use **Inter**. Font stack: "Suisseintltrial", "Inter", Arial, sans-serif.

| Recipe | Font | Weight | Size | Line height | Tracking | Use |
| --- | --- | --- | --- | --- | --- | --- |
| hero | Lyondisplay App | 300 | 96px | 1.15 | normal | Default opening headline, centered over two lines. |
| hero-italic | Lyondisplay App | 300 italic | 96px | 1.15 | normal | A meaningful phrase within the opening headline. |
| compact-display | Lyondisplay App | 300 | 96px | 0.9 | normal | Monumental closing conversion line; also alternate atmospheric openings. |
| compact-display-italic | Lyondisplay App | 300 italic | 96px | 0.9 | normal | Action word in the closing line and alternate openings. |
| chapter | Lyondisplay App | 300 | 80px | 1 | -0.01em | Major chapter heading. |
| chapter-italic | Lyondisplay App | 300 italic | 80px | 1 | -0.01em | Expressive verb or conclusion within chapter headings. |
| promise | Lyondisplay | 300 | 48px | 1.15 | normal | Compact statement in a wide proof band. |
| promise-italic | Lyondisplay | 300 italic | 48px | 1.15 | normal | Single stressed word in the promise. |
| scene-body | Inter | 300 | 20px | 1.35 | -0.01em | Support at the bottom of photographic feature panels. |
| card-title | Inter | 300 | 20px | 1.08 | -0.01em | Feature carousel titles beneath the media. |
| body | Inter | 300 | 16px | 1.5 | normal | Centered opening and chapter support. |
| card-body | Inter | 300 | 16px | 1.35 | -0.01em | Feature descriptions and plain review text. |
| action | Roboto Mono | 500 | 12px | 2 | 0.021em, uppercase | White conversion-button label. |
| secondary-action | Roboto Mono | 500 | 12px | 2 | normal, uppercase | Dark secondary-button label. |
| navigation | Roboto Mono | 400 | 12px | 1.5 | 0.021em, uppercase | Navigation labels. |
| footer-caption | Suisseintl | 300 | 12px | 1.41 | normal | Footer headings and newsletter support. |
| footer-link | Suisseintl | 300 | 12px | 2 | normal | Footer link columns. |

### Page-type variants

| Only for | Recipe | Font | Weight | Size | Line height | Tracking | Use |
| --- | --- | --- | --- | --- | --- | --- | --- |
| business proof sections | stat | Suisseintltrial | 400 | 50px | 1.1 | normal | Large quantified proof, left aligned. |
| institutional, business and focused feature sections | small-heading | Lyondisplay App | 300 | 38px | 0.9 | normal | Proof headings and focused feature card headings. |
| institutional, business and focused feature sections | small-heading-italic | Lyondisplay App | 300 italic | 38px | 0.9 | normal | Leading word in small serif headings. |
| business proof sections | stat-label | Suisseintl | 300 | 20px | 1.5 | normal | Description below a proof number. |
| institutional and business sections | business-body | Suisseintl | 300 | 18px | 1.5 | normal | Explanatory text and quotes. |
| institutional, business and focused feature pages | feature-body | Suisseintl | 300 | 16px | 1.5 | normal | Support paragraphs on alternate page types. |
| focused feature pages | question | Suisseintl | 400 | 16px | 1.5 | normal | Accordion questions. |
| institutional and business sections | business-label | Roboto Mono | 400 | 11px | 1.4 | 0.182em, uppercase | Long descriptive category captions inside cards. |

**Notes:**

- Use real italic spans within otherwise roman serif headings, not an entirely italic page or a second display family.
- The opening uses more leading than the closing line. Chapter type tightens tracking to -0.01em; body copy stays light.
- Do not carry tiny interface labels out into marketing paragraphs. The mono face marks navigation and actions, not prose.

## Layout

**Density:** Airy at the section level, finely packed inside product demonstrations; large empty black regions let the serif and small interfaces breathe.

### Spacing

| Value | Use |
| --- | --- |
| 12px | Feature title to paragraph; compact navigation and form spacing. |
| 16px | Alternate card-grid gutters. |
| 24px | Default paired-panel gutters, action spacing and card content separation. |
| 32px | Page side padding and alternate card interiors. |
| 40px | Internal separation in explanatory and stat cards. |
| 48px | Feature-card padding and larger inner gaps. |
| 60px | Footer column separation and FAQ outer padding. |
| 96px | Nature-panel inner padding and proof-band vertical padding. |
| 100px | Major section vertical padding and alternate heading-to-grid separation. |

### Containers and grids

- **wide-stage**: max width 1400px · side padding 32px. Nearly full-width media, cards and footer. At desktop the visible interior is 1376px.
- **reading-container**: max width 1200px · side padding 32px. Centered heading and explanation groups inside wider stages.
- **heading-wrap**: max width 980px. Centered chapter and compact-display title wrapping.
- **opening-wrap**: max width 830px. Default opening title and conversion stack.
- **support-wrap**: max width 420px. Short centered supporting paragraph.
- **nature-pair**: 2 columns · gap 24px. Equal nature and interface panels with alternating sides.
- **alternate-three-grid** (only for institutional and business pages): 3 columns · gap 16px. Equal explanatory cards.
- **business-four-grid** (only for business pages): 4 columns · gap 32px. Tall explanatory cards with compact mono captions.

## Corner radius

| Only for | Element | Radius |
| --- | --- | --- |
|  | actions | 8px |
|  | navigation | 12px |
|  | feature-panels | 24px |
|  | notification | 224px |
| institutional, business and focused feature sections | alternate-panels | 16px |
| business testimonials | business-quotes | 30px |

## Surfaces and depth

### Shadows

| Name | Value | Use |
| --- | --- | --- |
| notification-edge | `inset 0px 2px 0px -1px rgba(255,255,255,0.15)` | Subtle lit inner rim on stacked notification capsules. |

### Surfaces

#### Canvas · level 0

**Use:** Default page ground.

**CSS:** `background: #050505; color: #ffffff`

#### Quiet panel · level 1

**Use:** Feature cards and dark interface stages.

**CSS:** `background: rgba(255,255,255,0.04); border: 1px solid rgba(255,255,255,0.08); border-radius: 24px; overflow: hidden`

**Observed:** Subtle separation rather than an elevated drop shadow.

**Transfer:** Keep these panels dark even beside colorful photography.

#### Atmospheric seam · level 1

**Use:** Soft blue-violet light at a major chapter boundary.

**CSS:** `background: linear-gradient(70deg, #219ff8, #847dff 52%); filter: blur(50px); border-radius: 20%`

**Observed:** A very broad blurred shape is mostly clipped by a dark rounded wrapper; the visible result is a narrow luminous edge, not a full colored background.

**Transfer:** Place behind content and reveal only a small fraction at the edge of the stage.

#### Graphite panel · level 1

**Only for:** institutional, business and focused feature sections

**Use:** Explanatory, numerical and product demonstration cards.

**CSS:** `background: linear-gradient(135deg, #2b2b2c, #131313); background-color: #2e2e2e; border-radius: 16px`

**Observed:** A directional charcoal sheen; no prominent border or drop shadow.

**Transfer:** Use for information-heavy alternatives to the photographic chapters.

#### Business glass · level 2

**Only for:** business audience chapters

**Use:** Translucent explanatory tiles over blue atmospheric bands.

**CSS:** `background: linear-gradient(rgba(255,255,255,0.12), rgba(255,255,255,0)); border-radius: 16px; backdrop-filter: blur(24px); padding: 32px`

**Observed:** Two columns of light-to-transparent tiles, sometimes followed by one full-width tile.

**Transfer:** Keep the backing hue in the section, not in each card.

## Components

### Floating navigation

**Use:** Persistent global navigation.

**CSS:** `position: fixed; top: 24px; left: 24px; right: 24px; height: 56px; background: rgba(15,16,17,0.06); border-radius: 12px; backdrop-filter: blur(24px); z-index: 999`

**Text style:** `navigation` — Roboto Mono 400 · 12px/1.5 · tracking 0.021em · uppercase

**Observed:** Small symbol at left, compact centered navigation chips, login and a white conversion action at right. The scenery remains visible through the bar.

### Primary button

**Use:** Main conversion in navigation, opening and closing.

**CSS:** `display: inline-flex; gap: 4px; height: 48px; padding: 12px 18px; background: #ffffff; color: #000000; border-radius: 8px`

**Text style:** `action` — Roboto Mono 500 · 12px/2 · tracking 0.021em · uppercase

**Observed:** Content-width rectangular button with a small right arrow. Business variants change the label to a demo request.

**States:** Declared: hover background-color rgba(255,255,255,0.86) and outline 0px; focus-visible unknown; active outline 0px.

### Secondary button

**Use:** Explore a capability without competing with conversion.

**CSS:** `display: inline-flex; gap: 4px; height: 48px; padding: 12px 18px; background: rgba(255,255,255,0.1); color: #ffffff; border-radius: 8px`

**Text style:** `secondary-action` — Roboto Mono 500 · 12px/2 · uppercase

**Observed:** Centered beneath serif titles. Some wrappers add a faint traveling luminous edge.

**States:** Declared: hover background-color rgba(255,255,255,0.18) and outline 0px; focus-visible unknown; active outline 0px.

### Navigation chip

**Use:** Small global navigation items.

**CSS:** `display: flex; padding: 8px 12px; background: rgba(255,255,255,0.04); border-radius: 8px; color: #ffffff`

**Text style:** `navigation` — Roboto Mono 400 · 12px/1.5 · tracking 0.021em · uppercase

**Observed:** A plus symbol identifies the expandable product item; the active business item uses a brighter white tint.

**States:** Declared: hover background-color rgba(255,255,255,0.16) and outline 0px; focus-visible unknown; active outline 0px.

### Feature carousel card

**Use:** A browsable set of product capabilities.

**CSS:** `display: flex; flex-direction: column; gap: 24px; width: 420px; padding: 48px; background: rgba(255,255,255,0.04); border: 1px solid rgba(255,255,255,0.08); border-radius: 24px`

**Text style:** `card-title` — Inter 300 · 20px/1.08 · tracking -0.01em

**Observed:** Media comes first, then a short sans title and muted paragraph. Cards extend beyond the viewport with partial neighbors and circular arrow controls.

**Transfer:** Give each card a distinct interface fragment, not an icon above generic copy.

### Advice glass card

**Use:** A concrete outcome inside the opening product scene.

**CSS:** `padding: 24px; background: radial-gradient(100% 58.12% at 100% 100%, rgba(132,125,255,0.12) 0%, rgba(132,125,255,0) 100%), rgba(255,255,255,0.04); border: 1px solid rgba(255,255,255,0.08); border-radius: 24px; backdrop-filter: blur(24px)`

**Observed:** Small tracked mono category above a light serif outcome and a short sans explanation. Layered cards have varied opacity and slight rotation.

**Transfer:** Demonstrate one useful recommendation grounded in the new product rather than generic assistant chatter.

### Newsletter field

**Use:** Footer email signup.

**CSS:** `height: 52px; width: 400px; padding: 8px 12px 8px 22px; background: linear-gradient(0deg, #131313, #222222); background-color: #000000; border-radius: 8px`

**Observed:** A single email input with a small arrow submission control at the far right, beneath a compact heading and support line.

### Footer link

**Use:** Compact navigation columns.

**CSS:** `color: rgba(255,255,255,0.6); line-height: 24px`

**Text style:** `footer-link` — Suisseintl 300 · 12px/2

**Observed:** Text-only links under brighter small headings. Downloads and social destinations use separate gradient-backed icon buttons.

**States:** Declared: hover color rgb(255,255,255) and outline 0px; focus-visible unknown; active outline 0px.

### Explanatory card

**Only for:** institutional and business pages

**Use:** Describe principles or reasons to choose the product.

**CSS:** `display: flex; flex-direction: column; gap: 40px; padding: 32px; background: linear-gradient(135deg, #2b2b2c, #131313); border-radius: 16px`

**Text style:** `business-body` — Suisseintl 300 · 18px/1.5

**Observed:** A widely tracked mono caption leads, then a substantive paragraph; cards sit in equal-height rows.

### Stat card

**Only for:** business proof sections

**Use:** Quantified outcome proof.

**CSS:** `display: flex; flex-direction: column; gap: 40px; padding: 32px; background: linear-gradient(135deg, #2b2b2c, #131313); border-radius: 16px`

**Text style:** `stat` — Suisseintltrial 400 · 50px/1.1

**Observed:** Three cards across, left-aligned white sans numbers with subdued 20px descriptions. Numerals do not use the editorial serif.

### FAQ row

**Only for:** focused feature pages

**Use:** Practical objection handling.

**CSS:** `display: flex; justify-content: space-between; align-items: flex-start; gap: 24px; padding: 16px; background: rgba(255,255,255,0.04); border-radius: 8px`

**Text style:** `question` — Suisseintl 400 · 16px/1.5

**Observed:** Question at left, small plus at right; rows stack with 14px gaps inside a larger dark panel. The section title occupies a separate left column.

## Imagery and graphics

### Atmospheric nature

**Use:** Opening backdrop, paired feature chapters and conversion scenery.

**Observed:** Full-bleed blue sky with soft clouds opens the default composition. Elsewhere yellow flowers, luminous green meadow, orange dunes and defocused daisies occupy entire rounded panels. Text is white and centered over the image; natural detail stays behind it, while warm or green haze softens the lower area. Business openings use an aerial forest; focused features use sand.

**Transfer:** Choose close, immersive natural scenes with open areas behind the words, not people using laptops. Preserve organic texture and saturation; a sky photograph with a subtle tonal overlay is the minimum substitute for video.

**Build:** Use a cover image as the base, a dark translucent overlay for legibility, and a 541px circular glow near the lower center, blurred 60px. Match the glow to ochre, meadow green or earth. Clip all layers to the 24px panel; place copy above the glow.

### Device and interface theater

**Use:** Primary product proof in the opening and capability chapters.

**Observed:** A tall black, metallic-rimmed phone is front-facing in the default opening and appears diagonally in business proof. Dark interface fragments float individually inside near-black panels: graphs, account lists, score rings and recommendation cards. Media takes substantially more area than explanatory text.

**Transfer:** Use the new product's authentic interface. Without a device render, create a dark rounded frame around one crisp outcome and a few related data fragments; retain large negative space instead of reconstructing a whole dashboard.

### Luminous seams

**Use:** Abstract counterpart to nature photography at chapter boundaries and around selected actions.

**Observed:** Blue-violet glow grazes the edge of a rounded black stage. Secondary actions sometimes have a very faint moving white perimeter highlight.

**Transfer:** Retain a soft light seam on every new landing page even when imagery is replaced.

**Build:** For the stage, position a broad 1435px by 696px blue-to-violet gradient behind a clipped wrapper, blur 50px, and show only its edge. For a button rim, mask a conic gradient with white at 0deg and 360deg, transparent from 60deg through 310deg, down to a thin perimeter; add a second copy blurred 8px at opacity 0.12.

### Sculptural color fields

**Only for:** focused feature demonstrations

**Use:** Backdrops for selected two-up analysis cards.

**Observed:** Oversized violet, cyan, pink and lilac rounded shapes are clipped into 16px-corner panels around dark product illustrations.

**Transfer:** Keep this concentrated color treatment confined to a specific demonstration chapter rather than replacing the default dark page.

**Build:** Start with a purple panel; place pill-shaped layers larger than its bounds: broad violet diagonal, tall cyan and orchid columns, and a broad lilac oval. Rotate the violet form about -39deg, overlap dark canvas shapes to restore contrast, and clip to the panel. Preserve the soft blended impression; exact stops are not essential.

### Business testimonial color

**Only for:** business testimonials

**Use:** A conspicuously brighter proof strip.

**Observed:** Three 429px by 434px rounded cards use softly mottled color fields: blue into yellow, green into yellow-green, and cyan into pale aqua. White stars, centered quotation and small tracked uppercase attribution sit near the upper portion.

**Transfer:** Use verified short quotes with a generous lower color area; do not turn these into dark bordered review cards.

**Build:** Layer two or three large translucent radial gradients across a saturated base, using blur to remove hard edges; clip to 30px corners. Keep the white quote centered with 32px inner padding.

### Icons

**Observed:** Small white or subdued monochrome symbols accompany actions and controls: arrows, plus signs and platform marks. Most action icons are 16px–20px; standalone rating stars are about 24px. The aesthetic is thin and unobtrusive rather than illustrative.

**Transfer:** Use a consistent simple icon set with comparable optical scale. Keep service or integration identity within demonstrations, not as decorative marketing icons.

## Section recipes

How recurring sections are built.

### First screen: Sky and product opening

**Observed:** A full-bleed sky layer fills a 1232px opening, with Floating navigation above it. Center a compact offer capsule over a two-line 96px/1.15 light serif title in an 830px wrapper; one phrase is true italic. A muted 420px-wide support line follows with 24px separation, then Primary button. A front-facing 630px-wide black phone begins below the action and rises into the lower third of the first 900px. Its upper frame and large serif outcome are visible before the fold; its detailed glass advice cards and remaining body continue below. The background is visible around and through the product scene, not enclosed in a separate hero card.

**Transfer:** Stage a similarly oversized product or outcome object under a centered promise, with immersive sky or another natural atmosphere behind it. If there is no hardware, use a dark framed outcome panel of equivalent visual weight. Focused feature openings can instead fit within 900px over dunes, with a translucent question capsule below the action; business openings use forest imagery, a longer three-line 96px/0.9 title and small proof marks. Institutional openings are text-only on charcoal.

**Avoid:** Do not replace the opening with a left-aligned SaaS split layout, a tiled dashboard collage, or a small isolated device beside the headline.

**Text style:** `hero` — Lyondisplay App 300 · 96px/1.15

### Editorial chapter lead

**Observed:** A centered 80px/1 serif heading in the heading-wrap combines roman and italic. Support uses body or feature-body in a narrow centered column with 16px–24px separation. Secondary button may sit beneath; a major demonstration has roughly 100px of separation from the title group.

**Transfer:** Use a short verb and a concrete outcome; vary line breaks to keep the heading sculptural.

**Text style:** `chapter` — Lyondisplay App 300 · 80px/1 · tracking -0.01em

### Promise and validation band

**Observed:** Quiet panel spans the wide-stage with 96px vertical padding. A 48px/1.15 promise contains one italic word; muted support sits below. A row of monochrome award or certification marks adds validation without resembling a customer-logo billboard.

**Transfer:** Use authentic third-party proof or a concise trust statement in the same restrained space.

**Text style:** `promise` — Lyondisplay 300 · 48px/1.15

### Nature and interface pair

**Observed:** Nature-pair uses 676px by 631px panels at desktop. One panel has Atmospheric nature with an 80px headline near the top and 20px support near the bottom, padded 96px. Its partner is a Quiet panel with a small concentrated interface demonstration. Repeated pairs alternate sides with 24px gutters.

**Transfer:** Use each pair to explain an outcome and show its mechanism; retain the nature/interface contrast without requiring any particular number of pairs.

**Text style:** `chapter` — Lyondisplay App 300 · 80px/1 · tracking -0.01em

### Always-on notification stage

**Observed:** A near-black 900px stage centers an 80px serif heading above a stack of dark notification capsules. A small green status light sits above the title. The lead capsule is about 670px by 112px, uses a graphite vertical sheen, a lit inner rim and 224px radius; receding capsules shrink and dim.

**Transfer:** Show a concrete timely message from the product, with one clearly legible lead item and subdued layers behind it.

**Text style:** `chapter` — Lyondisplay App 300 · 80px/1 · tracking -0.01em

### Review wall

**Observed:** A plain three-column review wall on black uses five bright stars above each 16px quote and muted small attribution. There are no prominent card outlines. Business testimonial color is a distinct brighter alternative with carousel controls below.

**Transfer:** Use genuine quotations with varied lengths while preserving aligned columns and substantial vertical air.

**Text style:** `card-body` — Inter 300 · 16px/1.35 · tracking -0.01em

### Atmospheric closing call

**Observed:** A full-width image-backed conversion scene fades into Canvas. A small offer sits above a centered 96px/0.9 serif line with italic action word; proof marks and Primary button sit below. The default scene is 608px tall with 153px vertical padding. Alternate page types extend it to 1106px with a 432px by 552px phone below a short support line; the business version requests a demo rather than a download.

**Transfer:** Return to the opening atmosphere and make the next step unmistakable, using the compact-display pair. Keep an image-backed closing device even for products without a phone; substitute a product outcome object.

**Text style:** `compact-display` — Lyondisplay App 300 · 96px/0.9

### Utility footer

**Observed:** A wide dark footer uses 32px outer padding. A 400px newsletter column is followed by three compact navigation columns and a download/social column, separated with 60px gaps. Small gradient icon buttons group platform destinations. A modest wordmark sits below, then a 1px 14%-white divider and a dense subdued disclosure area.

**Transfer:** Scale disclosures to the actual product's needs; preserve the contrast between expansive conversion scenery and compact utility navigation.

**Text style:** `footer-caption` — Suisseintl 300 · 12px/1.41

### Business proof and audience panels

**Only for:** business pages

**Observed:** Stat card rows and Explanatory card grids follow centered chapter headings with 100px separation. Separate audience chapters sit in 16px rounded blue atmospheric bands with Business glass tiles in two columns; one band fades from deep blue into the dark ground.

**Transfer:** Use this business-oriented treatment for substantive evidence, segmentation or implementation reasons; keep numbers in the stat sans recipe.

**Text style:** `stat` — Suisseintltrial 400 · 50px/1.1

### Focused feature bento

**Only for:** focused feature pages

**Observed:** Graphite panel cards use 38px small-heading titles with italic leading words, narrow support and substantial centered interface artwork. Two equal tall cards can be followed by a wide shallow demonstration card. Sculptural color fields provide a contrasting chapter variant.

**Transfer:** Group related capabilities into a few substantial demonstrations rather than many small icon tiles.

**Text style:** `small-heading` — Lyondisplay App 300 · 38px/0.9

## Page flow

**Storyline:** Immersive promise → concrete product proof → capability outcomes → external or quantified validation → practical reassurance → atmospheric conversion → compact utility footer.

### Broad consumer product introduction

1. **promise** — Sky-backed centered serif promise and oversized phone outcome.
2. **validation** — Compact promise band with trust marks and company logos.
3. **capability overview** — Horizontal interface-first carousel.
4. **mechanism** — Always-on messages and alternating photographic/interface pairs.
5. **expanded use** — Full-width nature scene followed by conversational device proof.
6. **social proof** — Airy review wall.
7. **closing call** — Image-backed conversion scene and utility footer.

**Transfer:** Expand from a single vivid promise into useful outcomes, reserving atmospheric breathing room between dense demonstrations.

### Institutional credibility and approach

1. **promise** — Text-only centered editorial headline on charcoal.
2. **principles** — Three mono-captioned explanatory cards.
3. **validation** — Small serif investor heading and monochrome marks.
4. **participation** — Compact team invitation and secondary action.
5. **closing call** — Tall image-backed conversion with device, then shared footer.

**Transfer:** Keep this page shorter and more verbal while retaining mixed roman/italic titles and the atmospheric finish.

### Business buyer persuasion

1. **promise** — Aerial forest opening, longer title, demo action and awards.
2. **product demonstration** — Large diagonal device and company trust strip.
3. **business outcomes** — Blue atmospheric benefits band.
4. **quantified proof** — Three large sans-number stat cards.
5. **differentiation** — Four explanatory cards and nature-backed process panels.
6. **audience fit** — Blue glass-tile segmentation band.
7. **social proof** — Resource cards and colorful testimonial cards.
8. **closing call** — Tall demo conversion scene and utility footer.

**Transfer:** Replace consumer detail with measurable outcomes and audience fit while retaining nature as the emotional frame.

### Explain a focused product capability

1. **promise** — Dune-backed opening with compact title, offer, action and question capsule.
2. **capability proof** — Two-up graphite cards and one wide interface demonstration.
3. **interaction** — Question examples and a centered contextual demonstration.
4. **deeper value** — Sculptural color cards and a second two-up capability chapter.
5. **compatibility** — Orbiting integration marks around a small serif heading.
6. **objection handling** — Two-column FAQ panel.
7. **closing call** — Tall device conversion scene and utility footer.

**Transfer:** Let a focused feature carry richer interface demonstrations and practical questions rather than repeating a broad benefits wall.

## Writing

Warm, composed and outcome-led. Technical intelligence is described through useful personal consequences rather than mechanical complexity.

**Do not copy:** Do not reuse Origin's name, logo, proprietary artwork, quoted testimonials, award marks, regulated assertions, promotional pricing, user counts or performance claims. Replace all examples with accurate material for the new product. The examples below show the pattern only; write new copy for the new product.

- **Addressing the reader:** Address the reader as you and your. Use we for the product's helpful work; business copy shifts naturally to your employees or your team.
- **Rhythm:** Short verb-led headings, usually one sentence of support, then concrete proof. Longer business paragraphs remain direct and unhurried; exclamation marks are not a defining device.

| Role | Pattern | Typical length | Examples of the pattern |
| --- | --- | --- | --- |
| Opening headline | Introduce the product through an active relationship and a useful role; stress the differentiating phrase. | 5–10 words | “Meet your proactive AI Financial Advisor.” |
| Chapter heading | A verb plus a concrete object or outcome; the verb or final phrase carries italic emphasis. | 3–7 words | “Track your entire financial life.”<br>“Budgeting done for you” |
| Support | Explain what the product does and why the reader benefits, in one calm sentence. | 12–25 words | “We connect every piece of your finances to understand what matters most.” |
| Action | Short literal next step; exploration labels name the destination. | 2–4 words | “Get Started”<br>“Book a demo” |
| Concrete advice | Name an actionable situation, then quantify or explain the consequence. | 5–25 words | “Your money is sitting still.”<br>“Save $2,200 on childcare.” |
| Business proof caption | Translate measured results into an organizational outcome. | 4–9 words | “Boost productivity and prevent burnout” |

**Words and phrasing to use:** your; we; connect; track; understand; next move; in one place; personalized; clear; confidence

**Avoid:** disruptive ecosystem; 10x everything; revolutionary magic; crush your goals; AI jargon without an outcome

**Rules:**

- Use italic emphasis on the word carrying the promise, not on arbitrary decorative words.
- Pair broad claims with a visible mechanism, useful example or verified proof.
- Make demo questions sound like something a person would actually ask.
- Keep conversion labels literal and explanations human; uppercase is a visual treatment for controls, not the voice of paragraphs.

## Do's and Don'ts

### Do

- Keep default opening type at 96px/1.15 and chapter type at 80px/1.
- Use 24px gutters between nature/interface pairs and 32px desktop outer padding.
- Keep primary actions at 48px high with 8px corners.
- Give dark feature cards 4% white fill and an 8%-white 1px edge.
- Stage concentrated interface fragments inside substantial dark negative space.
- Use real nature texture and a soft lower glow rather than flat color behind every heading.
- Retain the image-backed 96px closing call and the blue-violet light seam.
- Keep business numerals in a 50px sans, separate from the serif headline system.

### Don't

- Do not set all marketing text in Roboto Mono; it belongs to markings and controls.
- Do not replace fine light serif titles with heavy geometric sans.
- Do not turn the white 8px-corner conversion action into a large colored pill.
- Do not use bright violet as the default page canvas or link color.
- Do not apply broad drop shadows to every card; separation comes from white-alpha edges and fills.
- Do not make every proof area a bordered testimonial card; the default review wall is plain on black.
- Do not use the 11px widely tracked business captions as ordinary body copy.
- Do not squeeze natural scenery into tiny thumbnails; it should fill a panel or backdrop.

## Motion and responsive

### Motion

**Observed:**

- Primary and secondary actions declare background-color transitions of 0.2s; navigation chips declare 0.3s cubic-bezier(0.455,0.03,0.515,0.955).
- Atmospheric mesh motion declares 24s ease-in-out infinite alternate. Border highlights declare borderTurn at 2.5s linear infinite.
- Circular scenes declare spin and counter-spin at 30s linear infinite; status lights declare 2s pulse cycles.

**Transfer:**

- Let slow atmosphere and faint rim motion support the static editorial composition; the core message and main outcome must remain legible without motion.
- Use the lead notification as the readable foreground and dim the receding stack.

**Unknown:**

- Scroll choreography, complete keyframe transforms and playback timing are not established.
- Focus-visible styling is not established.

### Responsive

**Observed:**

- Declared width breakpoints include 375px, 399px, 479px, 480px, 767px, 768px, 991px, 992px and 1230px; an additional pair of 1279px rules occurs in the default landing design.

**Transfer:**

- On narrow layouts, preserve the centered serif promise above the product object; reduce type to fit deliberate lines rather than retaining desktop overflow.
- Stack nature/interface pairs with their related content adjacent, retain rounded image panels, and keep the luminous seam clipped within the stage.
- Allow the interface carousel to show partial neighbors rather than shrinking all cards into miniature columns.

**Unknown:**

- Exact mobile type sizes, navigation collapse behavior and narrow-screen padding are not established.

## How to use this guide

1. Start from the Essentials and the first-screen recipe; where the guide offers variants, use the default named for a new marketing page.
2. Plan the page before building: map your content onto the section recipes and page flow, then check the plan against the Do's and Don'ts.
3. Build with the tokens and component specs; recreate signature graphics with their build recipes instead of generic decoration, and use the named font substitutes if a font is unavailable.
4. Render the page at desktop and mobile widths, compare it with the Essentials and the Don'ts, and fix the differences before you finish.
