Tel-U Endowment Aquarium — AI Context \& Development Guide

> Purpose: This document is the primary context/reference for AI-assisted development on the Unity client of Tel-U Endowment Aquarium.
> Audience: Unity/Game Programmer, Backend Engineer, Project Manager, Technical Artist, and AI coding assistants.
> Status: LoFi prototype / API integration preparation.
> Important: When requirements conflict with this document, the latest explicit project decision from the team takes precedence. Do not silently invent production behavior.

# 1\. Project Overview

Tel-U Endowment Aquarium is a web-based idle/simulation game inspired by *Insaniquarium*. It visualizes contributions to the Telkom University Endowment Fund as fish inside a personal aquarium.

A donor's donation history is translated into fish and fish rarity/tier. Players can care for their fish, customize their appearance, and view a collective "Big Aquarium" containing fish from multiple donors.

The project is intended to:

* Make donations feel more personal and tangible.
* Increase engagement through gamification.
* Encourage repeat donations through collection/progression.
* Provide an attractive interactive showcase for campus events/exhibitions.
* Demonstrate the collective contribution of the Endowment Fund community.

The game is primarily a "client-server WebGL application".

\---

# 2\. Core Architectural Principle

## Unity is a client, not the source of truth

The most important architecture rule is:

> \*\*Unity must not be the source of truth for donor, donation, ownership, rarity, or persistent player state. The backend API is the source of truth.\*\*

Unity is responsible for:

* Rendering.
* UI.
* Animation.
* Gameplay presentation.
* Local interaction.
* Sending player actions to the backend.
* Parsing API responses.
* Mapping backend IDs/codes to local game assets.

Backend is responsible for:

* Persistent player state.
* Donor data.
* Donation data.
* Fish ownership.
* Fish tier/rarity.
* Authentication/session handling.
* Validation.
* Feed cooldown enforcement.
* Appearance persistence.
* Big Aquarium selection.
* Integration with the official Endowment system.
* Database access.
* Security.

Database is **never accessed directly by Unity**.

\---

\#3 Architecture For LoFi (Prototype Demo)

Player Browser
|
v
itch.io
|
| loads Unity WebGL
v
Unity Game
|
v
Dummy Json Data

\#4 Unity Programmer Responsibilities

The Unity client should:

Initialize the game.
Check backend availability.
Authenticate/exchange access credentials when required.
Fetch player aquarium data.
Parse JSON responses.
Map backend codes to local assets.
Spawn fish.
Render fish tier/rarity.
Apply customization.
Handle feeding interaction.
Send feed requests.
Respect server responses and cooldowns.
Fetch and render Big Aquarium.
Render exhibition mode.
Handle settings and UI.
Provide loading/error/empty states.
Never make persistent-data decisions independently of backend.

The Unity client should not:

Query PostgreSQL directly.
Store authoritative donation amounts.
Decide donor tier from donation amount.
Trust client-provided fish ownership.
Trust client-provided rarity.
Permanently save authoritative progression only in PlayerPrefs.
Hardcode production donor data.
Reimplement backend business rules unless the behavior is purely visual/client-side.

\#5 Backend Responsibilities

backend is the bridge between Unity and persistent data.

Conceptually:

Official Endowment System
|
| webhook / API
v
Backend
|
v
PostgreSQL
|
| REST API
v
Unity

Backend should expose stable contracts to Unity even if the internal implementation changes.

For example:

Official Endowment API
|
v
Backend internal logic
|
v
Stable Unity-facing API
|
v
Unity

This means Unity should not need to know whether data came from:

PostgreSQL
Official Endowment API
Mock API
Webhook processing
Another internal service

\#6 Database

The project is designed around PostgreSQL.

The Unity application must not connect to PostgreSQL directly.

Correct:

Unity
|
v
REST API
|
v
PostgreSQL

Incorrect:

Unity
|
X
PostgreSQL

The database schema has been designed around entities such as:

Donor
Donation
Aquarium
Fish
Fish Species
Fish Tier
Access Token / authentication data
Webhook events
Game events

Some tables/features may be deferred during LoFi.

\#7 API Communication Model

The game uses REST API over HTTP/HTTPS.

Typical flow:

Unity
|
| GET /api/v1/me/aquarium
v
Backend
|
| query database
v
PostgreSQL
|
| result
v
Backend
|
| JSON
v
Unity
|
| deserialize
v
Game state / UI

Unity should use UnityWebRequest.

Example conceptual flow:

Request
|
+-- URL
+-- HTTP method
+-- headers
+-- optional body
|
v
Backend
|
v
HTTP status
|
v
JSON response
|
v
DTO
|
v
Domain/Game model
|
v
UI / GameObject

\#8 Current API Contract

The backend currently defines the following endpoints.

Game API
GET /api/v1/health
GET /api/v1/meta

POST /webhooks/v1/endowment

POST /api/v1/auth/exchange

GET /api/v1/me/aquarium

GET /api/v1/aquariums/:publicSlug

GET /api/v1/big-aquarium?limit=15

POST /api/v1/me/fish/:fishId/feed

PATCH /api/v1/me/fish/:fishId/appearance

GET /api/v1/aquariums/:publicSlug/events?after=0\&limit=100

Mock API
GET /health

POST /mock/v1/donations
GET /mock/v1/donations

POST /mock/v1/donations/:id/replay
POST /mock/v1/donations/:id/refund

POST /mock/v1/scenarios/common
POST /mock/v1/scenarios/rare
POST /mock/v1/scenarios/epic
POST /mock/v1/scenarios/legendary
POST /mock/v1/scenarios/random

Do not invent new endpoints unless the backend team agrees.

\#9 API Response Envelope

The backend uses a consistent JSON response envelope.

Successful response:

{
"schemaVersion": "1.0",
"requestId": "example-request-id",
"data": {}
}

Error response uses a consistent error structure defined by the backend contract.

Every request may include:

X-Request-ID: unique-request-id

If absent, the backend generates one automatically.

Unity should preserve/log requestId when useful for debugging.

\#10 API Security Rules

Public DTOs must never expose:

Donor email.
External donor ID.
Donation nominal amount.
Raw webhook payload.
Access-token hash.
Database credentials.

Unity should only receive the minimum data required for gameplay/presentation.

Do not add sensitive fields to Unity DTOs merely because they exist in the backend database.

\#11 Authentication

The project does not want a traditional username/password login flow for the short 5–10 minute web-game experience.

The backend currently has:

POST /api/v1/auth/exchange

The exact production authentication mechanism may involve a short-lived code, QR, token exchange, or another integration with the official system.

Important Unity rule:

Do not assume the final authentication UI/flow unless the backend contract explicitly defines it.

Authentication should be implemented behind an abstraction so the UI can change later.

Recommended conceptual interface:

IAuthService
|
+-- ExchangeCode(...)
+-- GetSession()
+-- ClearSession()

\#12 Fish Data Model

The important distinction is between species, tier, and appearance.

Conceptually:

Fish
|
+-- Species
|     +-- KOI
|     +-- GOLDFISH
|     +-- ...
|
+-- Tier
|     +-- COMMON
|     +-- RARE
|     +-- EPIC
|     +-- LEGENDARY
|
+-- Appearance
+-- color
+-- accessory
+-- other cosmetic data

Backend determines authoritative ownership and tier.

Unity determines how those codes are visually represented.

\#13 Donation Tier System

Current design example:

Donation	Tier	Visual
Rp 50.000 – Rp 500.000	Common	Standard color, small
Rp 500.001 – Rp 5.000.000	Rare	Unique color/pattern, medium
Rp 5.000.001 – Rp 25.000.000	Epic	Glow/particle, large

> Rp 25.000.000	Legendary	Exclusive design, special animation

These values are examples only.

Finance/Endowment must finalize the actual thresholds.

Unity must not calculate tier from donation amount.

Backend should send something like:

{
"tierCode": "EPIC"
}

Unity maps:

EPIC
-> Epic visual treatment
-> Epic prefab/config
-> Epic effects

\#14 Asset Mapping Rule

Backend sends identifiers/codes, not Unity assets.

Example:

{
"speciesCode": "KOI",
"tierCode": "EPIC",
"colorCode": "BLUE",
"accessoryCode": "CROWN"
}

Unity performs:

speciesCode
|
v
FishSpeciesCatalog
|
v
KOI -> Koi sprite/prefab/config

and:

accessoryCode
|
v
AccessoryCatalog
|
v
CROWN -> Crown sprite/prefab/config

Do not make backend send:

Fish.png
Crown.png

Asset paths are a Unity implementation detail.

\#15 Fish Cusstomization Architecture

Do not export a separate sprite for every fish + accessory combination.

Bad:

Koi\_Crown.png
Koi\_Hat.png
Koi\_Glasses.png
Goldfish\_Crown.png
Goldfish\_Hat.png
...

This creates combinatorial asset explosion.

Instead use layered composition:

Fish
|
+-- Body
|
+-- Accessory Anchor
|      |
|      +-- Accessory Sprite
|
+-- Face/Glasses Anchor
|
+-- Effect Anchor
|
+-- Name Tag

Example:

FishRoot
├── Body
├── Shadow
├── HatAnchor
├── FaceAnchor
├── EffectAnchor
└── NameTag

A fish can then dynamically display:

Body = Koi
Color = Blue
Hat = Crown
Glasses = None
Effect = EpicGlow

16. Fish Color

Prefer a neutral/white/grayscale base sprite where possible.

Example:

Fish\_Base\_White

Then Unity applies color:

spriteRenderer.color = selectedColor;

Do not create:

Koi\_Blue.png
Koi\_Red.png
Koi\_Green.png
...

unless the art direction specifically requires painted texture variants.

\#17 Fish Prefab Recommendation

Suggested structure:

FishPrefab
├── Body
│   └── SpriteRenderer
├── Shadow
│   └── SpriteRenderer
├── HatAnchor
│   └── \[dynamic accessory]
├── FaceAnchor
│   └── \[dynamic accessory]
├── EffectAnchor
│   └── \[dynamic effect]

Fish-specific anchors may differ.

For example:

Koi
HatAnchor = (x1, y1)

ClownFish
HatAnchor = (x2, y2)

The art team should define anchor points consistently.

\#18 Accessory Store

The store is a cosmetic system.

The backend should eventually determine:

Which accessories the player owns.
Which appearance is currently equipped.
Whether an item is unlocked/purchased.
Persistent appearance data.

Unity handles:

Store UI.
Preview.
Applying the visual locally after a successful response.
Displaying locked/unlocked state.
Mapping accessory codes to assets.

Example:

{
"accessoryCode": "HAT\_CROWN"
}

Unity:

HAT\_CROWN
|
v
AccessoryCatalog
|
v
Crown prefab/sprite

For persistence:

PATCH /api/v1/me/fish/:fishId/appearance

Do not assume the appearance was successfully saved until the backend confirms it.

\#19 Feed System

Current endpoint:

POST /api/v1/me/fish/:fishId/feed

The backend enforces a four-hour cooldown.

Important rule:

The client may display a local countdown for UX, but the backend is authoritative.

Correct:

Unity local timer
|
v
"Feed available in 03:12:00"
|
v
Player clicks Feed
|
v
POST /feed
|
v
Backend decides
|
+-- success
|
+-- cooldown/error

Do not rely solely on:

PlayerPrefs.lastFeedTime

because local storage can be manipulated and may become stale.

\#20 Big Aquarium

Endpoint:

GET /api/v1/big-aquarium?limit=15

Current intended behavior:

Display a random selection of aquarium.
Current target is 15.
Backend limits maximum requested count to 50.
One active fish per donor.
Other donors' fish are view-only.
Player cannot customize another donor's fish.
Donor name may appear subject to privacy requirements.
Big Aquarium is suitable for exhibition mode.

Unity should treat the returned list as authoritative.

Do not generate fake random fish in production when the endpoint is available.

\#21 Big Aquarium LoFi Without Real API

Before official integration, use:

Mock API

or local JSON fixture.

Recommended development approach:

BigAquariumService
|
+-- Real API implementation
|
+-- Mock implementation

Both should return the same client-side model.

Example:

IBigAquariumService
|
+-- ApiBigAquariumService
|
+-- MockBigAquariumService

This allows the Unity scene to be developed before the real backend is ready.

\#22 Exhibition Mode

Exhibition mode is designed for public displays.

Expected characteristics:

Minimal interaction.
Auto-play.
Large visual aquarium.
Fish may be smaller relative to the scene.
More background/space.
Donor names may be displayed.
Automatic refresh/rotation.
No need for visitors to log in manually.
Should recover gracefully from temporary API failure.

Potential flow:

Launch
|
v
Exhibition Mode
|
v
GET /api/v1/big-aquarium?limit=15
|
v
Spawn fish
|
v
Idle animation
|
v
Refresh after interval
|
v
Fetch again

\#23 Main Menu

Current likely structure:

MAIN MENU
│
├── Play / My Aquarium
├── Big Aquarium
├── Settings
└── About

The exact UI can change with design.

Settings should remain lightweight.

Recommended LoFi settings:

Audio

* Master Volume
* Music Volume
* SFX Volume

Language

* Indonesian
* English

About

* Version
* Credits

Do not add technical options that ordinary users do not need.

\#24 WebGL Considerationss

The project is intended to run in browsers.

Unity WebGL is still a Unity game build, but browser restrictions apply.

Important considerations:

API calls use browser networking.
HTTPS is strongly preferred.
CORS must allow the game origin.
Do not assume desktop filesystem access.
Do not use unsupported native APIs.
Avoid unnecessary synchronous blocking.
Memory usage matters.
Asset size matters.
Loading time matters.
Mobile/browser support requires testing.

Development origin might be:

http://localhost:xxxx

Production itch.io origin might be:

https://<project>.itch.io

Backend CORS must allow the appropriate origins.

\#25 Itch.io Architecture

For LoFi:

&#x20;                    Browser
                        |
                        v
                  itch.io page
                        |
                        v
                  Unity WebGL
                        |
                        | HTTPS REST
                        v
                Backend Server
                        |
                        v
                   PostgreSQL


Itch.io hosts the Unity WebGL files.

The backend can be hosted separately.

The browser does not care that the API is hosted separately as long as:

The API is reachable.
CORS is configured.
HTTPS/security is correct.
Authentication works.

\#26 Reccommended Unity Architecture

Do not put all API calls directly into UI scripts.

Avoid:

MainMenu.cs
-> UnityWebRequest
-> JSON parsing
-> database logic assumptions
-> UI
-> fish spawning

Instead separate responsibilities.

Recommended:

Presentation/UI
|
v
Application/Game Services
|
v
API Services
|
v
UnityWebRequest
|
v
Backend

Example:

UI
|
+-- AquariumController
|
+-- BigAquariumController
|
+-- SettingsController
|
v
Services
|
+-- PlayerService
+-- AquariumService
+-- FishService
+-- AuthService
+-- BigAquariumService
|
v
ApiClient
|
v
UnityWebRequest

\#27 DTO vs Domain Model

Do not tightly couple backend JSON objects to visual GameObjects.

Recommended:

JSON
|
v
DTO
|
v
Domain/Game Model
|
v
Presenter/Controller
|
v
GameObject

Example DTO:

\[Serializable]
public class FishDto
{
public string id;
public string speciesCode;
public string tierCode;
public string colorCode;
public string accessoryCode;
}

Then a domain model may be:

public class FishData
{
public string Id { get; }
public FishSpecies Species { get; }
public FishTier Tier { get; }
public Color Color { get; }
public string AccessoryCode { get; }
}

The exact implementation can vary.

\#28 Do Not Let API JSON Control Unity Asset Paths

Bad:

{
"spritePath": "Assets/Fish/Koi.png"
}

Good:

{
"speciesCode": "KOI"
}

Unity owns the asset mapping.

This allows artists to reorganize assets without breaking the backend.

\#31 ScriptableObject Catalogs

For asset mapping, ScriptableObjects are recommended.

Example:

FishSpeciesCatalog
├── KOI
│   ├── Sprite
│   ├── Prefab
│   └── config
├── GOLDFISH
│   └── ...
└── ...

Accessory catalog:

AccessoryCatalog
├── HAT\_CROWN
├── HAT\_TOPHAT
├── GLASSES\_ROUND
└── ...

Tier catalog:

TierCatalog
├── COMMON
├── RARE
├── EPIC
└── LEGENDARY

This reduces hardcoded switch statements.

\#32 API Client

Use a single reusable HTTP layer where practical.

Conceptual responsibilities:

ApiClient

* GET
* POST
* PATCH
* common headers
* authentication header
* request ID
* timeout
* response parsing
* error handling

Higher-level services should call the API client.

Example:

FishService
|
v
ApiClient.Post("/api/v1/me/fish/.../feed")

Avoid creating custom HTTP behavior in every MonoBehaviour.

\#33 Loading States

Every API-dependent screen should have clear states:

Idle
Loading
Success
Empty
Error
Retrying

Example:

Open Aquarium
|
v
Loading...
|
+---- Success ----> Show aquarium
|
+---- Empty ------> Show empty state
|
+---- Error ------> Show retry UI

Do not freeze the entire game while waiting for a network request.

\#34 Error Handling

At minimum distinguish:

Network Error
Server Error
Authentication Error
Validation Error
Cooldown / Gameplay Error
Not Found
Timeout
Unknown Error

The player should receive friendly messages.

Do not show raw backend stack traces to users.

For debugging, log:

Endpoint
HTTP method
status code
requestId
safe error code

Never log:

access tokens
passwords
database credentials
sensitive donor information

\#35. Async Pattern

API calls should not block the main thread.

Use Unity-supported asynchronous patterns appropriate to the project's Unity version.

Typical conceptual flow:

Coroutine / async operation
|
v
UnityWebRequest.SendWebRequest()
|
v
await / yield
|
v
parse response
|
v
update main-thread game state

Avoid:

while (!request.isDone) {}

because it can freeze the game.

\#36. Caching

Cache only what is appropriate.

Potentially cache:

Static metadata.
Fish species definitions.
Accessory definitions.
UI configuration.

Be cautious with:

Player aquarium.
Fish appearance.
Feed cooldown.
Donation-derived data.

Authoritative player data should be refreshed when needed.

\#37. Local Persistence

PlayerPrefs can be used for non-sensitive client preferences:

Master Volume
Music Volume
SFX Volume
Language

Do not use PlayerPrefs as the authoritative database for:

Donation
Fish ownership
Tier
Feed eligibility
Purchased accessories

Those belong to the backend.

\#39. Recommended Persistent Services

Possible persistent services:

GameBootstrap
ApiClient
AuthService
PlayerService
SettingsService
AudioService
SceneService

Do not make every system a singleton by default.

Use dependency injection or explicit references if the project's complexity justifies it.

\#40. Mock Mode

Because LoFi development may happen before the real Endowment integration is available, the Unity client should support mock data.

Potential configuration:

Backend Mode:
Mock
Development API
Production API

Do not hardcode URLs throughout scripts.

Use one configuration source.

For example:

ApiConfig

* baseUrl
* timeout
* useMock

For production, do not accidentally ship:

localhost

\#41. Example API Base URL Architecture

Instead of:

UnityWebRequest.Get("http://localhost:3000/api/v1/me/aquarium");

prefer:

ApiConfig.baseUrl
+
"/api/v1/me/aquarium"

Then:

Development:
http://localhost:3000

Staging:
https://staging-api.example.com

Production:
https://api.example.com

The actual URLs must come from the backend/deployment team.

\#42. API Contract Workflow

When backend changes an endpoint:

1. Backend changes contract
|
v
2. Update API documentation/schema
|
v
3. Notify Unity team
|
v
4. Update DTO/service if needed
|
v
5. Test integration

Do not silently change field names.

Example:

speciesCode

must not suddenly become:

species\_id

without coordination.

\#49. AI Rules

This section is specifically for AI coding assistants.

Rule 1 — Inspect before modifying

Before writing code:

Inspect the existing scripts.
Inspect scene hierarchy.
Inspect existing managers.
Inspect namespaces.
Inspect current Unity version.
Inspect existing API code.
Reuse existing architecture where possible.

Do not create duplicate systems.

Rule 2 — Do not rewrite working systems unnecessarily

If a current system already works:

Do not replace it just because a different architecture looks cleaner.

Make the smallest safe change.

Rule 3 — Do not invent API fields

Only use fields supported by:

API contract.
OpenAPI.
Backend source.
Explicit project requirements.

If a required field is missing, identify the mismatch instead of inventing it.

Rule 4 — Keep backend concerns out of Unity

Unity should not contain:

SQL
PostgreSQL credentials
database schema logic
donation tier calculation
webhook validation
HMAC verification

Those belong to backend.

Rule 5 — Keep asset concerns out of backend

Backend should not know:

Assets/Fish/Koi.prefab
Assets/UI/FishIcon.png

Use stable codes:

KOI
CROWN
EPIC
Rule 6 — Prefer small changes

For a bug:

Inspect
|
Identify root cause
|
Smallest fix
|
Compile
|
Test

Do not rewrite the whole architecture.

Rule 7 — Preserve WebGL compatibility

Avoid platform-specific code unless explicitly guarded.

Be careful with:

File I/O.
Threads.
Native DLLs.
Direct sockets.
System APIs.
Desktop-only input.
Unsupported filesystem assumptions.
Rule 8 — Use defensive null handling

Network data may be:

null
empty
missing
delayed
malformed
outdated

Handle these states gracefully.

Rule 9 — Do not hide errors

Bad:

catch
{
}

Prefer logging safe diagnostic information.

Rule 10 — Do not expose secrets

Never put:

API secret
database password
webhook secret
private token

into client-side Unity code.

Anything shipped in WebGL should be considered inspectable by the user.

\#60. Quick Reference for AI

Before changing anything, remember:

PROJECT
Tel-U Endowment Aquarium

CLIENT
Unity WebGL

HOSTING
itch.io

COMMUNICATION
REST API / HTTPS

BACKEND
Node.js / Express-style API

DATABASE
PostgreSQL

SOURCE OF TRUTH
Backend

AUTHORITY
Donation -> Backend
Tier -> Backend
Ownership -> Backend
Cooldown -> Backend
Appearance persistence -> Backend

UNITY
UI
Gameplay presentation
Fish movement
Animation
Asset mapping
Customization rendering
API consumption
Local settings

API
/api/v1/...

MAIN GAME ENDPOINTS
GET  /api/v1/health
GET  /api/v1/meta
POST /api/v1/auth/exchange
GET  /api/v1/me/aquarium
GET  /api/v1/aquariums/:publicSlug
GET  /api/v1/big-aquarium?limit=15
POST /api/v1/me/fish/:fishId/feed
PATCH /api/v1/me/fish/:fishId/appearance
GET  /api/v1/aquariums/:publicSlug/events

MOCK ENDPOINTS
GET  /health
POST /mock/v1/donations
GET  /mock/v1/donations
POST /mock/v1/donations/:id/replay
POST /mock/v1/donations/:id/refund
POST /mock/v1/scenarios/common
POST /mock/v1/scenarios/rare
POST /mock/v1/scenarios/epic
POST /mock/v1/scenarios/legendary
POST /mock/v1/scenarios/random

RESPONSE ENVELOPE
{
schemaVersion,
requestId,
data
}

ASSET RULE
Backend sends codes.
Unity maps codes to assets.

FISH CUSTOMIZATION
Layered sprites / prefab components.
Do not create every fish+accessory combination.

SECURITY
Never ship secrets in WebGL.

LOFI
Mock API first -> Unity integration -> real backend -> itch.io.

PRIMARY RULE
Backend owns data.
Unity owns experience.

