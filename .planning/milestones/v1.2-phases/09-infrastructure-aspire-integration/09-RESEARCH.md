# Phase 9: Infrastructure & Aspire Integration - Research

**Researched:** 2026-02-13
**Domain:** Nuxt 4 frontend integration with .NET Aspire orchestration
**Confidence:** HIGH

## Summary

This phase establishes the foundation for the v1.2 Web Dashboard by creating a Nuxt 4 frontend project and integrating it with the existing .NET Aspire orchestration. The research reveals that modern Aspire (v13+) has first-class JavaScript/TypeScript support through the `Aspire.Hosting.JavaScript` package (formerly `Aspire.Hosting.NodeJs`), making it straightforward to orchestrate Nuxt dev servers alongside .NET services.

Nuxt 4 (launched mid-2025) is a stability-focused release with excellent TypeScript support, Vite as the default bundler, and seamless integration with Nuxt UI and Tailwind CSS. The architecture uses Nitro as its server engine, which provides built-in support for API proxying via `routeRules`, eliminating CORS issues during development. API key authentication can be implemented using event handler utilities that validate headers before processing requests, following the explicit validation pattern recommended by the Nuxt community.

**Primary recommendation:** Use `AddNpmApp` (or newer `AddViteApp` if using Aspire 13+) to orchestrate the Nuxt dev server, configure API proxying via Nuxt's `routeRules` for development, and implement API key validation using explicit utility functions in server API routes rather than global middleware.

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Nuxt | 4.x | Full-stack Vue framework with SSR/SSG | Official Vue.js framework, industry standard for production Vue apps in 2026 |
| Nuxt UI | v3/v4 | Vue UI component library (Reka UI + Tailwind) | Official Nuxt component library, modern design system with excellent DX |
| Tailwind CSS | v4.x | Utility-first CSS framework | Industry standard, v4 is current in 2026, zero-config with Vite |
| TypeScript | 5.x | Type-safe JavaScript | Built-in Nuxt 4 support with auto-imports and strict mode |
| Aspire.Hosting.JavaScript | 13.0+ | Node.js/npm orchestration in Aspire | First-class JavaScript support in Aspire 13, renamed from Aspire.Hosting.NodeJs |
| Vite | Latest | Build tool and dev server | Default bundler for Nuxt 4, HMR in dev + production optimization |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| @nuxtjs/tailwindcss | Latest | Tailwind CSS integration module | Alternative to Tailwind v4 Vite plugin if needing Tailwind v3 compatibility |
| nuxt-auth-utils | Latest | Session and auth utilities | Optional helper for session management, can build custom validation instead |
| h3 | Latest | HTTP framework (Nitro foundation) | Automatically included, used for event handlers and server utilities |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Nuxt UI | Tailwind UI, shadcn-vue, PrimeVue | Nuxt UI is officially maintained, has best Nuxt integration, component detection works out of box |
| Aspire.Hosting.JavaScript | Docker Compose, manual npm scripts | Aspire provides unified dashboard, service discovery, telemetry, health checks - much better DX |
| routeRules proxy | Standalone proxy server, nginx | routeRules works in development automatically, no extra infra needed |
| Custom API key validation | nuxt-auth-utils, Auth0 | For simple API key auth, custom validation is lighter; use auth libs for OAuth/sessions |

**Installation:**

```bash
# Create Nuxt 4 project
npx nuxi@latest init dashboard

# Install dependencies
cd dashboard
npm install @nuxt/ui tailwindcss

# Add Aspire.Hosting.JavaScript to AppHost
dotnet add TradingBot.AppHost package Aspire.Hosting.JavaScript --version 13.0.1
```

## Architecture Patterns

### Recommended Project Structure

```
/Users/baotoq/Work/trading-bot/
├── TradingBot.ApiService/        # .NET API service
├── TradingBot.AppHost/           # Aspire orchestration
│   ├── AppHost.cs                # Add Nuxt app here
│   └── TradingBot.AppHost.csproj
├── TradingBot.ServiceDefaults/
├── dashboard/                    # NEW: Nuxt 4 frontend
│   ├── app/
│   │   └── app.vue               # Root component with <UApp>
│   ├── assets/
│   │   └── css/
│   │       └── main.css          # Tailwind + Nuxt UI imports
│   ├── server/
│   │   ├── api/                  # API endpoints (protected with API key)
│   │   ├── middleware/           # Server middleware (avoid global auth)
│   │   └── utils/                # Auth validation utilities
│   ├── nuxt.config.ts            # Modules, routeRules, runtimeConfig
│   ├── package.json              # Dev script for Aspire
│   └── tailwind.config.ts        # Optional Tailwind customization
└── tests/
```

### Pattern 1: Aspire Orchestration with AddNpmApp

**What:** Add Nuxt dev server to Aspire AppHost using `AddNpmApp` method
**When to use:** Development orchestration for unified service management
**Example:**

```csharp
// Source: https://aspire.dev/integrations/frameworks/javascript/
// TradingBot.AppHost/AppHost.cs

var dashboard = builder.AddNpmApp("dashboard", "../dashboard", "dev")
    .WithHttpEndpoint(port: 3000, env: "PORT")
    .WithEnvironment("NUXT_API_ENDPOINT", apiService.GetEndpoint("http"))
    .WithReference(apiService)
    .WaitFor(apiService);
```

**Key methods:**
- `AddNpmApp(name, directory, scriptName)` - Runs `npm run {scriptName}`
- `WithHttpEndpoint(port, env)` - Allocates port and sets environment variable
- `WithEnvironment(key, value)` - Custom environment variables
- `WithReference(service)` - Enables service discovery, auto-injects connection strings
- `WaitFor(service)` - Ensures dependency starts first

**Alternative (Aspire 13+):**
```csharp
var dashboard = builder.AddViteApp("dashboard", "../dashboard", "dev")
    .WithHttpEndpoint(port: 3000, env: "VITE_PORT")
    .WithReference(apiService);
```

### Pattern 2: API Proxy Configuration with routeRules

**What:** Proxy API requests to backend to avoid CORS issues during development
**When to use:** Development environment, frontend needs to call backend API
**Example:**

```typescript
// Source: https://nitro.build/config#routerules
// dashboard/nuxt.config.ts

export default defineNuxtConfig({
  modules: ['@nuxt/ui'],

  css: ['~/assets/css/main.css'],

  runtimeConfig: {
    // Server-side only (never exposed to client)
    apiKey: process.env.NUXT_API_KEY || '',

    // Public variables (exposed to client)
    public: {
      apiEndpoint: process.env.NUXT_API_ENDPOINT || 'http://localhost:5000'
    }
  },

  routeRules: {
    // Proxy all /api/** requests to backend
    '/api/**': {
      proxy: `${process.env.NUXT_API_ENDPOINT || 'http://localhost:5000'}/api/**`,
      cors: true
    }
  },

  devtools: { enabled: true }
})
```

**Important notes:**
- `routeRules` proxy only works in development by default
- Use environment variables for flexibility across environments
- CORS is handled automatically by proxy in dev
- In production, configure proper CORS headers on .NET API

### Pattern 3: API Key Authentication with Explicit Validation

**What:** Validate API key header in protected endpoints using utility functions
**When to use:** Dashboard API endpoints requiring authentication
**Example:**

```typescript
// Source: https://masteringnuxt.com/blog/protecting-server-routes
// dashboard/server/utils/auth.ts

import { H3Event } from 'h3'

export function requireApiKey(event: H3Event): void {
  const config = useRuntimeConfig(event)
  const authHeader = getHeader(event, 'x-api-key')

  if (!authHeader || authHeader !== config.apiKey) {
    throw createError({
      status: 403,
      statusText: 'Forbidden',
      message: 'Invalid or missing API key'
    })
  }
}
```

```typescript
// dashboard/server/api/portfolio.get.ts

export default defineEventHandler(async (event) => {
  // Explicit validation - clear what runs when
  requireApiKey(event)

  // Fetch from backend API using internal service discovery
  const config = useRuntimeConfig(event)
  const data = await $fetch(`${config.public.apiEndpoint}/api/portfolio`, {
    headers: {
      'x-api-key': config.apiKey
    }
  })

  return data
})
```

**Why this pattern over middleware:**
- Explicit: Clear which routes are protected
- Flexible: Can pass arguments to validation functions
- Testable: Easy to unit test utility functions
- Recommended by Nuxt community over global server middleware

### Pattern 4: Error Handling in Event Handlers

**What:** Use `createError` to return proper HTTP status codes
**When to use:** Authentication failures, validation errors, not found
**Example:**

```typescript
// Source: https://nuxt.com/docs/4.x/api/utils/create-error

// 401 Unauthorized - missing/invalid credentials
throw createError({
  status: 401,
  statusText: 'Unauthorized',
  data: { reason: 'API key required' }
})

// 403 Forbidden - valid credentials but insufficient permissions
throw createError({
  status: 403,
  statusText: 'Forbidden',
  message: 'Access denied'
})

// 404 Not Found
throw createError({
  status: 404,
  statusText: 'Not Found'
})
```

**Important:** In API routes, use `statusText` and `data` properties - the `message` property doesn't automatically propagate to clients.

### Pattern 5: Package.json Script Configuration

**What:** Define npm scripts that Aspire will execute
**When to use:** Aspire orchestration with AddNpmApp
**Example:**

```json
// dashboard/package.json
{
  "name": "trading-bot-dashboard",
  "type": "module",
  "private": true,
  "scripts": {
    "dev": "nuxt dev --port 3000",
    "build": "nuxt build",
    "generate": "nuxt generate",
    "preview": "nuxt preview",
    "postinstall": "nuxt prepare"
  },
  "dependencies": {
    "@nuxt/ui": "^3.0.0",
    "tailwindcss": "^4.0.0",
    "vue": "latest"
  },
  "devDependencies": {
    "@nuxt/devtools": "latest",
    "nuxt": "^4.0.0",
    "typescript": "latest"
  }
}
```

### Anti-Patterns to Avoid

- **Global server middleware for auth:** Creates hidden complexity, use explicit utility functions instead
- **CORS configuration in production via routeRules:** routeRules proxy is dev-only; configure CORS on .NET API for production
- **Hardcoded API URLs:** Always use environment variables or runtimeConfig
- **Using AddNodeApp for Nuxt:** Use AddNpmApp (runs npm scripts) or AddViteApp, not AddNodeApp (runs node directly)
- **Exposing API key to client:** API key should be in `runtimeConfig` (server-only), not `runtimeConfig.public`
- **Manual Tailwind config for Nuxt UI:** v4 with Vite plugin needs minimal config, module handles setup

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Node.js orchestration | Custom docker-compose for Nuxt | Aspire.Hosting.JavaScript | Unified dashboard, health checks, telemetry, service discovery built-in |
| API proxying in dev | Custom Express proxy server | Nuxt routeRules with proxy | Zero config, automatic CORS handling, Nitro handles it |
| Session management | Custom JWT signing/validation | nuxt-auth-utils | Secured sealed cookies, automatic expiry, well-tested |
| Component library | Custom UI components | Nuxt UI | Maintained, accessible, Tailwind-based, auto-imported |
| Environment config | Manual process.env checks | Nuxt runtimeConfig | Type-safe, validated at build time, server/client separation |
| TypeScript setup | Manual tsconfig | Nuxt auto-config | Auto-imports work out of box, paths configured correctly |

**Key insight:** Nuxt 4 and Aspire 13 both provide comprehensive solutions for common infrastructure problems. Building custom solutions adds maintenance burden and misses edge cases that official tools handle (health checks, HMR, service discovery, telemetry integration, etc.).

## Common Pitfalls

### Pitfall 1: CORS Issues with Custom Environment Variable Names

**What goes wrong:** JavaScript frameworks expect specific environment variable prefixes (e.g., `VITE_*`, `NUXT_*`). Using `WithReference()` generates generic names like `services__api__http__0` which may not be accessible client-side.

**Why it happens:** Aspire's automatic environment variable injection doesn't account for framework-specific naming conventions. Vite only exposes variables prefixed with `VITE_` to the client.

**How to avoid:**
1. Use `WithEnvironment()` explicitly for client-side variables with proper prefixes
2. Use `runtimeConfig` in Nuxt to manage server/client environment separation
3. For server-to-server calls, Aspire's auto-injected variables work fine

**Warning signs:**
- `undefined` when accessing `process.env` in client code
- CORS errors when frontend tries to call backend API
- Environment variables visible in server logs but not in browser

**Example solution:**
```csharp
var dashboard = builder.AddNpmApp("dashboard", "../dashboard", "dev")
    .WithEnvironment("NUXT_PUBLIC_API_ENDPOINT", apiService.GetEndpoint("http"))
    .WithEnvironment("NUXT_API_KEY", builder.AddParameter("apiKey", secret: true))
    .WithReference(apiService); // Still useful for service discovery in production
```

### Pitfall 2: Tailwind IntelliSense Not Working in VSCode

**What goes wrong:** Tailwind CSS IntelliSense extension doesn't provide autocomplete in Nuxt UI components or `ui={}` props.

**Why it happens:**
1. Nuxt generates Tailwind config dynamically in `.nuxt/tailwind/postcss.mjs`
2. VSCode IntelliSense looks for `tailwind.config.js` by default
3. IntelliSense doesn't recognize Vue component prop patterns by default

**How to avoid:**
1. Create `.vscode/settings.json` in project root:
```json
{
  "tailwindCSS.experimental.configFile": ".nuxt/tailwind/postcss.mjs",
  "tailwindCSS.classAttributes": ["class", "className", "ui"],
  "files.exclude": {
    ".nuxt": true
  }
}
```
2. Run `npx nuxi prepare` to regenerate Nuxt types
3. Restart VSCode completely
4. Install Tailwind CSS IntelliSense extension

**Warning signs:**
- No autocomplete for Tailwind classes
- Classes show as unknown in editor
- IntelliSense works in regular HTML but not in Vue components

### Pitfall 3: API Routes Return 500 Instead of 401/403

**What goes wrong:** Authentication errors return generic 500 errors instead of proper 401/403 status codes.

**Why it happens:**
1. Throwing generic errors without status codes
2. Using `message` property in `createError` for API routes (doesn't propagate to client)
3. Uncaught exceptions in validation logic

**How to avoid:**
1. Always use `createError` with explicit `status` and `statusText`
2. Use `data` property for additional error information in API routes
3. Wrap validation in try-catch if needed
4. Test error responses with curl or Postman

**Warning signs:**
- Generic "Internal Server Error" messages
- No useful error information in client console
- Authentication errors logged but not returned properly

**Example:**
```typescript
// BAD - returns 500
if (!apiKey) {
  throw new Error('Missing API key')
}

// GOOD - returns 403
if (!apiKey) {
  throw createError({
    status: 403,
    statusText: 'Forbidden',
    data: { reason: 'Missing API key' }
  })
}
```

### Pitfall 4: Aspire Health Checks Fail for Nuxt Dev Server

**What goes wrong:** Aspire marks Nuxt service as unhealthy even though dev server is running.

**Why it happens:**
1. Nuxt dev server doesn't expose a `/health` endpoint by default
2. Aspire tries to health check the service and fails
3. Service starts but Aspire dashboard shows "Unhealthy"

**How to avoid:**
1. Don't add health checks for dev servers (not needed in development)
2. If needed, create a simple health endpoint in `server/api/health.get.ts`:
```typescript
export default defineEventHandler(() => {
  return { status: 'healthy' }
})
```
3. Configure health check in Aspire:
```csharp
.WithHttpHealthCheck("/api/health")
```

**Warning signs:**
- Aspire dashboard shows Nuxt service as unhealthy
- Service works fine but has red status indicator
- Logs show health check timeout errors

### Pitfall 5: Production Build Uses Dev Proxy Configuration

**What goes wrong:** Application tries to use `routeRules` proxy in production, which doesn't work.

**Why it happens:**
1. `routeRules` proxy is development-only by default
2. Production build needs proper CORS headers on API server
3. Environment variables point to wrong endpoints

**How to avoid:**
1. Configure CORS on .NET API for production (allow dashboard origin)
2. Use different `NUXT_PUBLIC_API_ENDPOINT` in production (full URL)
3. Test production build locally: `npm run build && npm run preview`
4. Document that proxy is dev-only in comments

**Warning signs:**
- Works in dev but not in production preview
- CORS errors in production but not development
- API calls fail after `npm run build`

**Example production configuration:**
```typescript
// nuxt.config.ts
export default defineNuxtConfig({
  routeRules: {
    // Only active in development
    '/api/**': {
      proxy: process.env.NODE_ENV === 'development'
        ? `${process.env.NUXT_API_ENDPOINT}/api/**`
        : undefined
    }
  }
})
```

## Code Examples

Verified patterns from official sources:

### Complete Aspire AppHost Configuration

```csharp
// Source: https://aspire.dev/integrations/frameworks/javascript/
// TradingBot.AppHost/AppHost.cs

using Serilog;
using Serilog.Templates;
using Serilog.Templates.Themes;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Aspire apphost");

    var builder = DistributedApplication.CreateBuilder(args);

    builder.Services.AddSerilog((services, lc) => lc
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(new ExpressionTemplate(
            "[{@t:HH:mm:ss} {@l:u3}{#if @tr is not null} ({substring(@tr,0,4)}:{substring(@sp,0,4)}){#end}] {@m}\n{@x}",
            theme: TemplateTheme.Code)));

    var postgres = builder
        .AddPostgres("postgres")
        .WithLifetime(ContainerLifetime.Persistent)
        .WithDataVolume()
        .WithPgAdmin(c => c.WithLifetime(ContainerLifetime.Persistent).WithHostPort(5050));

    var postgresdb = postgres.AddDatabase("tradingbotdb");

    var redis = builder.AddRedis("redis")
        .WithLifetime(ContainerLifetime.Persistent)
        .WithDataVolume()
        .WithRedisInsight(c => c.WithLifetime(ContainerLifetime.Persistent).WithHostPort(5051));

    var redisHost = redis.Resource.PrimaryEndpoint.Property(EndpointProperty.Host);
    var redisPort = redis.Resource.PrimaryEndpoint.Property(EndpointProperty.Port);

    var pubSub = builder
        .AddDaprPubSub("pubsub")
        .WithMetadata("redisHost", ReferenceExpression.Create($"{redisHost}:{redisPort}"))
        .WaitFor(redis);
    if (redis.Resource.PasswordParameter is not null)
    {
        pubSub.WithMetadata("redisPassword", redis.Resource.PasswordParameter);
    }

    var apiService = builder.AddProject<Projects.TradingBot_ApiService>("apiservice")
        .WithReference(postgresdb)
        .WithReference(redis)
        .WithDaprSidecar(sidecar =>
        {
            sidecar.WithReference(pubSub);
        })
        .WithHttpHealthCheck("/health");

    // NEW: Add Nuxt dashboard
    var apiKey = builder.AddParameter("dashboardApiKey", secret: true);

    var dashboard = builder.AddNpmApp("dashboard", "../dashboard", "dev")
        .WithHttpEndpoint(port: 3000, env: "PORT")
        .WithEnvironment("NUXT_PUBLIC_API_ENDPOINT", apiService.GetEndpoint("http"))
        .WithEnvironment("NUXT_API_KEY", apiKey)
        .WithReference(apiService)
        .WaitFor(apiService);

    builder.Build().Run();

    Log.Information("Stopped cleanly");

    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "An unhandled exception occurred during bootstrapping");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
```

### Complete Nuxt Configuration

```typescript
// Source: https://nuxt.com/docs/4.x/api/nuxt-config
// dashboard/nuxt.config.ts

export default defineNuxtConfig({
  modules: ['@nuxt/ui'],

  css: ['~/assets/css/main.css'],

  runtimeConfig: {
    // Server-side only (never exposed to client)
    apiKey: process.env.NUXT_API_KEY || '',

    // Public variables (exposed to client)
    public: {
      apiEndpoint: process.env.NUXT_PUBLIC_API_ENDPOINT || 'http://localhost:5000'
    }
  },

  routeRules: {
    // Development proxy to avoid CORS issues
    '/api/**': {
      proxy: process.env.NODE_ENV === 'development'
        ? `${process.env.NUXT_PUBLIC_API_ENDPOINT || 'http://localhost:5000'}/api/**`
        : undefined,
      cors: true
    }
  },

  devtools: { enabled: true },

  typescript: {
    strict: true,
    typeCheck: false // Set to true for CI/CD
  },

  compatibilityDate: '2024-11-01'
})
```

### Tailwind CSS + Nuxt UI Setup

```css
/* dashboard/assets/css/main.css */
@import "tailwindcss";
@import "@nuxt/ui";
```

```vue
<!-- dashboard/app/app.vue -->
<template>
  <UApp>
    <NuxtLayout>
      <NuxtPage />
    </NuxtLayout>
  </UApp>
</template>

<script setup lang="ts">
// UApp wrapper required for Toast, Tooltip, and programmatic overlays
</script>
```

### Protected API Endpoint with API Key Validation

```typescript
// Source: https://masteringnuxt.com/blog/protecting-server-routes
// dashboard/server/utils/auth.ts

import { H3Event } from 'h3'

export function requireApiKey(event: H3Event): void {
  const config = useRuntimeConfig(event)
  const authHeader = getHeader(event, 'x-api-key')

  if (!authHeader) {
    throw createError({
      status: 401,
      statusText: 'Unauthorized',
      data: { reason: 'API key required' }
    })
  }

  if (authHeader !== config.apiKey) {
    throw createError({
      status: 403,
      statusText: 'Forbidden',
      data: { reason: 'Invalid API key' }
    })
  }
}
```

```typescript
// dashboard/server/api/portfolio.get.ts

export default defineEventHandler(async (event) => {
  // Explicit validation - clear and testable
  requireApiKey(event)

  // Fetch from backend API
  const config = useRuntimeConfig(event)

  try {
    const data = await $fetch(`${config.public.apiEndpoint}/api/portfolio`, {
      headers: {
        'x-api-key': config.apiKey
      }
    })

    return data
  } catch (error) {
    throw createError({
      status: 502,
      statusText: 'Bad Gateway',
      data: { reason: 'Failed to fetch from backend API' }
    })
  }
})
```

### Health Check Endpoint for Aspire

```typescript
// dashboard/server/api/health.get.ts

export default defineEventHandler(() => {
  return {
    status: 'healthy',
    timestamp: new Date().toISOString()
  }
})
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Aspire.Hosting.NodeJs | Aspire.Hosting.JavaScript | Aspire 13 (2025) | Renamed for clarity, supports more than Node.js, includes Vite/Deno support |
| AddNpmApp with args parameter | AddNpmApp without args + WithEnvironment | Aspire 9 → 13 | More explicit, clearer configuration, avoids command injection |
| Tailwind CSS v3 with postcss | Tailwind CSS v4 with Vite plugin | Tailwind v4 (2024) | Zero-config, faster builds, no postcss.config.js needed |
| Nuxt 3 with auto-imports | Nuxt 4 with enhanced TypeScript | Nuxt 4 (mid-2025) | Better type inference, stability improvements, Vite performance gains |
| Global server middleware for auth | Explicit utility functions | Community best practice | Clearer intent, easier testing, avoids hidden complexity |
| @nuxtjs/proxy module | Built-in routeRules proxy | Nuxt 3+ | No external module needed, Nitro handles it natively |

**Deprecated/outdated:**
- `@nuxtjs/proxy` module: Use `routeRules` with `proxy` option in Nuxt 3+
- `AddNodeApp(..., args: string[])`: Use `WithEnvironment` and `WithRunScript` in Aspire 13+
- Tailwind `postcss.config.js` with `@nuxtjs/tailwindcss`: Use Tailwind v4 Vite plugin for zero-config
- Global `serverMiddleware` config: Use `server/middleware/` directory with explicit utility functions

## Open Questions

1. **API Key Storage and Rotation**
   - What we know: Aspire supports `AddParameter(secret: true)` for secrets, stored in user-secrets in dev
   - What's unclear: Production secret management strategy (Azure Key Vault? Kubernetes secrets? Environment variables?)
   - Recommendation: Plan for environment variable-based secrets in production, document in deployment guide

2. **Production Deployment Strategy for Nuxt**
   - What we know: Nuxt 4 can build to static, Node.js server, or serverless
   - What's unclear: Target deployment platform (Docker? Azure App Service? Static hosting?)
   - Recommendation: Start with Node.js server mode (default), defer deployment strategy to later phase

3. **WebSocket Support for Live Updates**
   - What we know: Nuxt supports WebSockets via Nitro, SignalR available in .NET
   - What's unclear: Whether live updates will use WebSockets or polling
   - Recommendation: Start with polling (simpler), add WebSockets in future phase if needed

4. **API Versioning Strategy**
   - What we know: .NET API currently has no versioning
   - What's unclear: Whether to implement API versioning now or later
   - Recommendation: Defer versioning until v1.3, use `/api/dashboard/*` prefix for dashboard endpoints

## Sources

### Primary (HIGH confidence)

- [Aspire JavaScript Integration Official Docs](https://aspire.dev/integrations/frameworks/javascript/) - AddNpmApp, AddNodeApp, WithEnvironment patterns
- [Aspire Add to Existing App Guide](https://aspire.dev/get-started/add-aspire-existing-app/) - Installation and configuration steps
- [Nuxt UI Installation](https://ui.nuxt.com/docs/getting-started/installation/nuxt) - Module setup, Tailwind integration
- [Nuxt Configuration Reference](https://nuxt.com/docs/4.x/api/nuxt-config) - routeRules, runtimeConfig
- [Nitro routeRules Documentation](https://nitro.build/config#routerules) - Proxy configuration syntax
- [Nuxt createError Utility](https://nuxt.com/docs/4.x/api/utils/create-error) - Error handling patterns
- [Nuxt Sessions and Authentication Recipe](https://nuxt.com/docs/4.x/guide/recipes/sessions-and-authentication) - Auth patterns with event handlers

### Secondary (MEDIUM confidence)

- [Aspire for JavaScript Developers Blog](https://devblogs.microsoft.com/aspire/aspire-for-javascript-developers/) - Overview of JavaScript support in Aspire 13
- [.NET Aspire with React/NextJS Medium Article](https://medium.com/@adamtrip/net-aspire-with-react-nextjs-or-any-other-node-js-ef99f398815f) - Real-world integration examples
- [Protecting Server Routes - Mastering Nuxt](https://masteringnuxt.com/blog/protecting-server-routes) - Auth patterns and best practices
- [Server Middleware is an Anti-Pattern - Mastering Nuxt](https://masteringnuxt.com/blog/server-middleware-is-an-anti-pattern-in-nuxt) - Why to avoid global middleware
- [Adding Environment Variables to Aspire Services](https://timheuer.com/blog/add-environment-variables-to-aspire-services) - WithEnvironment patterns

### Tertiary (LOW confidence - flagged for validation)

- Community discussions on Tailwind IntelliSense issues in Nuxt
- GitHub issues regarding CORS and proxy configuration
- Medium articles on Nuxt 4 authentication patterns

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - All libraries verified via official docs, Aspire 13 confirmed via Microsoft docs
- Architecture: HIGH - Patterns verified in official Nuxt 4 and Aspire documentation
- Pitfalls: MEDIUM-HIGH - Mix of official docs and community best practices, cross-verified where possible

**Research date:** 2026-02-13
**Valid until:** ~2026-03-31 (30 days for stable tech, longer if no major releases)

**Notes:**
- Nuxt 4 launched mid-2025, considered stable as of Feb 2026
- Aspire 13 is current version, JavaScript support is first-class
- Tailwind CSS v4 is current, zero-config setup is standard
- All package versions confirmed via official sources or NuGet/npm registries
