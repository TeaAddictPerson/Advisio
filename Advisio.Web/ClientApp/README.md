# Advisio React Frontend

This is the React/TypeScript frontend for the Advisio application.

## Setup

1. Install dependencies:
```bash
npm install
```

## Development

Run the development server:
```bash
npm run dev
```

The React app will run on `http://localhost:5173` with Vite's dev server, which proxies API requests to the ASP.NET Core backend.

## Building for Production

Build the React app:
```bash
npm run build
```

This will create the production build in `wwwroot/dist`, which will be served by the ASP.NET Core application.

## Project Structure

- `src/components/` - Reusable React components
- `src/components/ui/` - Shadcn UI components
- `src/pages/` - Page components
- `src/lib/` - Utility functions
- `public/` - Static assets

## Integration with ASP.NET Core

The React app communicates with the ASP.NET Core backend through the `/api` endpoints. The backend serves the built React app from `wwwroot/dist` in production.

