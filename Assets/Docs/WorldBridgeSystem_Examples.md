# WorldBridgeSystem Example Uses

This file shows practical example patterns for the current `WorldBridgeSystem` in:

- `FracturedStudios.Invoker.WorldBridgeSystem`
- `FracturedStudios.Utils.ExpressionHelpers`

The examples here are based on the current implementation in `Assets/Scripts/EventInvokers/WorldBridgeSystem.cs`.

---

## 1. Register and fetch an object by ID

Use this when you want a stable string ID for a `GameObject` or `Component`.

```csharp
using UnityEngine;
using FracturedStudios.Invoker;

public class BridgeRegisterExample : MonoBehaviour
{
    [SerializeField] private string bridgeId = "library.librarian";

    private void OnEnable()
    {
        if (WorldBridgeSystem.Instance != null)
            WorldBridgeSystem.Instance.RegisterID(bridgeId, gameObject);
    }

    private void OnDisable()
    {
        if (WorldBridgeSystem.Instance != null)
            WorldBridgeSystem.Instance.UnregisterID(bridgeId);
    }
}
```

Fetch later:

```csharp
var librarian = WorldBridgeSystem.Instance?.GetByID<GameObject>("library.librarian");
if (librarian != null)
{
    Debug.Log("Found librarian: " + librarian.name);
}
```

---

## 2. Call a method by registered ID

Use this when you want decoupled scene communication and can tolerate reflection.

Target component:

```csharp
using UnityEngine;

public class AlarmTarget : MonoBehaviour
{
    public void TriggerAlarm(string source, float loudness)
    {
        Debug.Log($"Alarm from {source}, loudness={loudness}");
    }
}
```

Caller:

```csharp
WorldBridgeSystem.Instance?.CallMethodByID(
    "library.alarm",
    "TriggerAlarm",
    "restricted_area",
    0.9f);
```

Notes:

- `CallMethodByID` uses reflection.
- Method name must match exactly.
- Argument count and types must match the target method.

---

## 3. Read or write a field/property by ID

This is useful for lightweight debugging or generic tooling.

Write a member:

```csharp
bool changed = WorldBridgeSystem.Instance?.SetValueByID(
    "player.main",
    "moveSpeed",
    7.5f) ?? false;
```

Read a member:

```csharp
object value = WorldBridgeSystem.Instance?.GetValueByID("player.main", "moveSpeed");
if (value != null)
{
    Debug.Log("moveSpeed=" + value);
}
```

Notes:

- The member can be a field or property.
- Properties must be writable for `SetValueByID` and readable for `GetValueByID`.
- This path is reflection-based, so use direct references for hot loops.

---

## 4. Register an event invoker

Use this when multiple systems need to react to the same event key.

```csharp
using System;
using UnityEngine;
using FracturedStudios.Invoker;

public class BridgeEventExample : MonoBehaviour
{
    private IDisposable _token;

    private void OnEnable()
    {
        if (WorldBridgeSystem.Instance == null)
            return;

        _token = WorldBridgeSystem.Instance.RegisterInvoker(
            "library.light.changed",
            OnLightChanged,
            id: "library.listener.light-debug");
    }

    private void OnDisable()
    {
        _token?.Dispose();
    }

    private void OnLightChanged(object[] args)
    {
        Debug.Log("Light changed event received: " + args.Length);
    }
}
```

Raise the event:

```csharp
WorldBridgeSystem.Instance?.InvokeKey("library.light.changed", 0.82f, 0.18f, true);
```

---

## 5. Register an event that returns a value

Use `RegisterInvokerReturn` when callers need answers back.

```csharp
using System;
using UnityEngine;
using FracturedStudios.Invoker;

public class BridgeQueryExample : MonoBehaviour
{
    private IDisposable _token;

    private void OnEnable()
    {
        if (WorldBridgeSystem.Instance == null)
            return;

        _token = WorldBridgeSystem.Instance.RegisterInvokerReturn(
            "player.query.is_crouching",
            QueryCrouchState,
            id: "player.query.provider");
    }

    private void OnDisable()
    {
        _token?.Dispose();
    }

    private object QueryCrouchState(object[] args)
    {
        return true;
    }
}
```

Read the first response:

```csharp
object result = WorldBridgeSystem.Instance?.InvokeKeyReturnFirst("player.query.is_crouching");
if (result is bool isCrouching)
{
    Debug.Log("Is crouching: " + isCrouching);
}
```

Read all responses:

```csharp
object[] responses = WorldBridgeSystem.Instance?.InvokeKeyReturn("player.query.is_crouching")
    ?? Array.Empty<object>();
```

---

## 6. Use `PayInvoke` for keyed paid routes

This depends on the configured behavior of `DynamicDictionaryInvoker`, but the wrapper is available on `WorldBridgeSystem`.

```csharp
bool paid = WorldBridgeSystem.Instance?.PayInvoke(
    "shop.purchase",
    token: "coins",
    "flash_upgrade",
    100) ?? false;
```

---

## 7. Remove all handlers registered under an entry ID

This is useful when one system registered multiple handlers and you want to clean them up together.

```csharp
int removed = WorldBridgeSystem.Instance?.RemoveAllEntriesForId("library.listener.light-debug") ?? 0;
Debug.Log("Removed handlers: " + removed);
```

Also note that `UnregisterID` already tries to remove all invoker entries using the same entry ID.

---

## 8. Tie background or async work to bridge lifetime

Use the shutdown token so work stops when the bridge is destroyed.

```csharp
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using FracturedStudios.Invoker;

public class BridgeShutdownExample : MonoBehaviour
{
    private async void Start()
    {
        var bridge = WorldBridgeSystem.Instance;
        if (bridge == null)
            return;

        CancellationToken token = bridge.GetShutdownToken();

        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(250, token);
                Debug.Log("Polling while bridge is alive");
            }
        }
        catch (TaskCanceledException)
        {
        }
    }
}
```

---

## 9. Use `ExpressionHelpers` for boxed member access

This is useful when you want reusable compiled accessors instead of repeated reflection.

```csharp
using System;
using FracturedStudios.Utils;

Type playerType = typeof(ThirdPersonBasic);
Func<object, object> getter = ExpressionHelpers.CreateBoxedGetter(playerType, "moveSpeed");
Action<object, object> setter = ExpressionHelpers.CreateBoxedSetter(playerType, "moveSpeed");

object boxedPlayer = ThirdPersonBasic.Instance;
object speedBefore = getter(boxedPlayer);
setter(boxedPlayer, 6.5f);
object speedAfter = getter(boxedPlayer);
```

Typed version:

```csharp
Func<ThirdPersonBasic, float> getter = ExpressionHelpers.CreateGetter<ThirdPersonBasic, float>("moveSpeed");
Action<ThirdPersonBasic, float> setter = ExpressionHelpers.CreateSetter<ThirdPersonBasic, float>("moveSpeed");
```

---

## 10. Read shifted bits through the world bridge

This is the main integration point between `ExpressionHelpers` and `WorldBridgeSystem`.

Example: read 3 bits starting at bit 4 from a registered field/property path.

```csharp
using System;
using FracturedStudios.Utils;

Func<string, int> getter = ExpressionHelpers.CreateWorldShiftedGetter("someFlags", 4, 3);
int value = getter("player.main");
Debug.Log("Shifted value: " + value);
```

You can also build the getter directly for a known type:

```csharp
Func<object, int> getter = ExpressionHelpers.GetOrCreateShiftedGetter(
    typeof(MyFlagHolder),
    "stateMask",
    8,
    4);
```

Requirements:

- The member must resolve to an integral or enum value.
- The object ID must already be registered in `WorldBridgeSystem`.

---

## 11. Example `IWorldBridgeRegistrable` component

The interface is defined on `WorldBridgeSystem`. It does not auto-register by itself, but it is useful as a consistent contract.

```csharp
using UnityEngine;
using FracturedStudios.Invoker;

public class BridgeRegistrableExample : MonoBehaviour, WorldBridgeSystem.IWorldBridgeRegistrable
{
    [SerializeField] private string bridgeId = "npc.librarian";

    public string BridgeId => bridgeId;
    public string BridgeGroup => "npc";
    public object BridgeMetadata => new { kind = "librarian", scene = gameObject.scene.name };

    private void OnEnable()
    {
        var bridge = WorldBridgeSystem.Instance;
        if (bridge == null)
            return;

        bridge.RegisterID(BridgeId, gameObject);
        OnRegistered(bridge);
    }

    private void OnDisable()
    {
        var bridge = WorldBridgeSystem.Instance;
        if (bridge == null)
            return;

        OnUnregistered(bridge);
        bridge.UnregisterID(BridgeId);
    }

    public void OnRegistered(WorldBridgeSystem bridge)
    {
        Debug.Log("Registered: " + BridgeId);
    }

    public void OnUnregistered(WorldBridgeSystem bridge)
    {
        Debug.Log("Unregistered: " + BridgeId);
    }
}
```

---

## 12. Practical patterns for this project

Good fits in this repo:

- Register the player as `player.main` and query `moveSpeed` or crouch state.
- Register the librarian as `library.librarian` for light/debug event routing.
- Use invoker keys like `library.light.changed`, `debug.placement_failed`, or `player.query.is_crouching`.
- Use `CreateWorldShiftedGetter` when a registered object stores state in an enum or bitmask and you want a cheap reusable reader.

Less ideal fits:

- Per-frame reflection with `CallMethodByID`, `SetValueByID`, or `GetValueByID` in tight gameplay loops.
- Using string IDs where a direct serialized reference already exists and is stable.

---

## 13. Common pitfalls

- `WorldBridgeSystem.Instance` can be `null` early in startup or in test scenes.
- `GetByID<T>` only succeeds if the registered object actually matches `T`.
- `CallMethodByID` fails silently if the method name is wrong or the signature does not match.
- `SetValueByID` and `GetValueByID` only resolve a single member name, not a dot path.
- `CreateWorldShiftedGetter` returns `0` if the ID is missing or the bridge is unavailable.
- Invoker registrations should usually be disposed in `OnDisable` or `OnDestroy`.
