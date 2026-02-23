# Lost Spawns
Lost Spawns (aka Lost) is a voxel based 3D post apocolyptic survival game mimicing the play style, crafting system, physics, weapons, survival mechanics, building mechanics, vehicle mechanics, weather systems, animals, trees, theme, etc. of the DayZ survival game.

This game showcases the power of Blazor Wasm and SpawnDev.ILGPU for high performance 3D graphics, physics, GPU compute, and peer-to-peer networking.

## Phase 1
- A voxel based 3D post apocolyptic survival game mimicing the play style, crafting system, physics, weapons, survival mechanics, building mechanics, vehicle mechanics, weather systems, animals, trees, theme, etc. of the DayZ survival game.
- SpawnDev.BlazorJS is used for all Javascript - Blazor Wasm interop using strongly typed wrappers. If an expected class or class member is not supported or working corectly we can fix SpawnDev.BlazorJS to support it.
- Use SpawnDev.ILGPU and C# kernels for the voxel engine and entire GPU rendering pipeline avoiding any unnecessary .Net <-> Javascript, and GPU <-> CPU data transfers and interop.
- C# Kernels: Use ILGPU for Frustum Culling, Occlusion Culling, and Particle Physics.
- Storage Buffers: Keep your entire scene graph in a GPUBuffer.
- Draw Indirect: Have your ILGPU kernels fill a WebGPU Indirect Buffer. This allows the GPU to tell itself what to draw, bypassing the WASM-to-JS bottleneck.
- Settings to control various aspects of the game including Video settings, Rendering settings, etc
- Each game instance creates unique ECDSA and ECDH keys and saves them in an IndexedDB for reuse once created to prove identify to other peer instances.
- Users can set the player skin and model, and name.
- Keyboard and Mouse, and Controller support
- Users can create new worlds and edit them in "Editor Mode" with god mode and editor tools. Worlds can be be generated using "seeds". Various biomes and landscapes should be supported.
- Eventual WebXR mode to allow playing the game using VR

## Phase 2 (still planning - do not do yet)
- Multiplayer support via SpawnDev.BlazorJS.PeerJS and/or SpawnDev.BlazorJS.SocketIO and WebRTC (additional multiplayer details).
- Lobby shows Community servers andOfficial servers. Users can also host their own servers and share them with friends or the public.
- Official servers are hosted by us and have a persistent world that is always online. Players can join and leave the official servers at any time, and their progress will be saved. Official servers will have regular events and updates to keep the community engaged.
- Community servers are hosted by players and can be either persistent or non-persistent. Persistent community servers will save player progress, while non-persistent servers will not. Players can create and manage their own community servers, and they can choose to make them public or private.
- Players can form clans and groups to play together on official and community servers. Clans can have their own private servers, and they can also participate in events and competitions on official servers.
- Cross-platform play will be supported, allowing players on different devices to play together seamlessly. This includes support for PC, consoles, and mobile devices.
- In-game voice chat and text chat will be implemented to enhance communication between players. This will allow players to coordinate strategies, socialize, and build a stronger community within the game.
- Regular updates and expansions will be released to keep the game fresh and engaging. These updates may include new content, features, and improvements based on player feedback and community input. We will also host seasonal events and limited-time challenges to keep players coming back for more.
- We will also implement a robust reporting and moderation system to ensure a safe and enjoyable gaming environment for all players. This will include tools for reporting inappropriate behavior, as well as a team of moderators to review reports and take appropriate action when necessary.
- Overall, our goal is to create a vibrant and thriving multiplayer community within Lost Spawns, where players can connect, collaborate, and compete in a rich and immersive post-apocalyptic world.
- Eventually we can add mod support to allow the community to create and share their own content, such as new weapons, vehicles, maps, and game modes. This will help to keep the game fresh and engaging for players, and it will also foster creativity and innovation within the community. We can provide tools and resources to help modders get started, and we can also host a platform for sharing and discovering mods created by the community.
- We can also explore the possibility of cross-platform play with other games in the same genre, such as DayZ. This would allow players from different games to interact and play together, creating a larger and more diverse player base. We can work with the developers of other games to implement cross-platform functionality and ensure a seamless experience for players across different platforms.
- In addition to multiplayer features, we can also focus on improving the single-player experience by adding more content, such as new weapons, vehicles, and locations to explore. We can also enhance the AI of NPCs and animals to create a more immersive and challenging gameplay experience. Regular updates and expansions will help to keep the game fresh and engaging for players, whether they prefer single-player or multiplayer modes.
- Overall, our vision for Lost Spawns is to create a rich and immersive post-apocalyptic survival game that offers both a compelling single-player experience and a vibrant multiplayer community. We will continue to listen to player feedback and make improvements based on their suggestions, ensuring that Lost Spawns remains an enjoyable and engaging game for years to come.
- 


