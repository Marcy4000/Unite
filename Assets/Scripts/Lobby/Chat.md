# Party chat

`ChatManager` uses the existing `ChatHolder` UI. Global chat contains only current-party broadcasts; private chat contains only the conversation with the selected party member. The player dropdown is disabled in global mode and when there are no eligible private recipients. Local players and bots are excluded from that dropdown, and selection is preserved by player ID when the roster changes.

## Opening and configuring chat

```csharp
chatManager.OpenGlobalChat();
chatManager.OpenPrivateChat(player.Id);
chatManager.OpenChat();
chatManager.CloseChat();
```

- `OpenGlobalChat()` opens the global party conversation explicitly.
- `OpenPrivateChat(string playerId)` opens private chat with that current-party player. Unknown, departed, local, and bot IDs are ignored without opening a different conversation.
- `OpenChat()` follows an active notification to its conversation; otherwise it reopens the current configuration.
- `SetChatChannel(ChatManager.ChatChannel channel)` changes the mode without opening the menu.
- `SelectPrivatePlayer(int index)` selects a player by dropdown index. Prefer `OpenPrivateChat(player.Id)` for external player menus because roster indices can change.
- `CurrentChannel` and `SelectedPrivatePlayerId` expose the current configuration.

The lobby scene assigns `globalChatToggle`, `privateChatToggle`, and `privatePlayerDropdown`. Listeners are registered at runtime; the prefab does not need persistent toggle/dropdown callbacks.

## Delivery and unread messages

Messages are submitted to the host, which validates party membership, party ID, and the sender's network client ID against the party's `OwnerID` property. Global messages are delivered only to connected human party members. Private messages are delivered only to the sender and recipient, never broadcast for other clients to filter. The host remains the trusted relay and can process their contents.

Global and private histories remain separate. Only messages in the visible conversation are marked read; messages for other conversations continue to increment the unread badge. Clicking an active preview opens its conversation. Changing or leaving the party clears all local history and unread state. History is local to the session and is not replayed to late joiners.

Run `Unite.LobbyChat.Tests` in EditMode for channel, roster, unread-state, scene-wiring, and server-routing coverage. Run `Unite.LobbyChat.PlayMode.Tests` in PlayMode for real host/client delivery and private-message isolation checks.
