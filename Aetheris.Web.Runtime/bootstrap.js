// The npm facade starts the runtime explicitly. This file is the SDK build entrypoint.
import { dotnet } from './_framework/dotnet.js';
await dotnet.create().then(runtime => runtime.runMain());
