# Sonrisa Event Alert System Frontend

This project was generated using [Angular CLI](https://github.com/angular/angular-cli) version 22.2.2.

## Development server

Run the backend with its HTTPS launch profile from the repository root so the configured Angular proxy can reach it:

```powershell
dotnet run --project backend/Sonrisa.EventAlertSystem.Api --launch-profile https
```

Initialize the local database once from the API project directory:

```powershell
cd backend/Sonrisa.EventAlertSystem.Api
dotnet ef database update
```

Then start Angular in a second terminal:

```powershell
cd frontend
npm ci
npm start
```

Open `http://localhost:4200`. The development proxy forwards `/api` to `https://localhost:7126`; certificate validation is disabled only for this local proxy. Apply the backend EF Core migrations explicitly before using the database-backed screens.

The selected `demo-user` or `demo-admin` identity is stored in local storage and sent as `X-Demo-Identity`. This demo mechanism is not suitable for public deployment.

## Code scaffolding

Angular CLI includes powerful code scaffolding tools. To generate a new component, run:

```bash
ng generate component component-name
```

For a complete list of available schematics (such as `components`, `directives`, or `pipes`), run:

```bash
ng generate --help
```

## Building

To build the project run:

```bash
ng build
```

This will compile your project and store the build artifacts in the `dist/` directory. By default, the production build optimizes your application for performance and speed.

## Running unit tests

To execute unit tests with the [Vitest](https://vitest.dev/) test runner, use the following command:

```bash
ng test
```

## Running end-to-end tests

For end-to-end (e2e) testing, run:

```bash
ng e2e
```

Angular CLI does not come with an end-to-end testing framework by default. You can choose one that suits your needs.

## Additional Resources

For more information on using the Angular CLI, including detailed command references, visit the [Angular CLI Overview and Command Reference](https://angular.dev/tools/cli) page.
