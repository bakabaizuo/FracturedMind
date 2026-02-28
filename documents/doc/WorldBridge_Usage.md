# WorldBridge & Invoker Usage Examples

Below are concise, copy-pasteable examples for `WorldBridgeSystem`, `DynamicDictionaryInvoker`, and `ExpressionHelpers`.

## Register / invoke (void)

Register and dispose a handler:

```csharp
// register
IDisposable token = WorldBridgeSystem.Instance.RegisterInvoker(
    "player_damaged",
    args => {
        var damage = (int)args[0];
        Debug.Log($"Player took {damage} damage");
    },
    DynamicDictionaryInvoker.Layer.Func,
    id: "player_1"
);

// invoke
WorldBridgeSystem.Instance.InvokeKey("player_damaged", 12);

// later: unregister
token.Dispose();
```

## Register / invoke (returnable)

Register a returnable handler and collect results:

```csharp
// register returnable
IDisposable retToken = WorldBridgeSystem.Instance.RegisterInvokerReturn(
    "compute_answer",
    args => {
        // compute and return an object
        return 42;
    });

// invoke all returnables
object[] results = WorldBridgeSystem.Instance.InvokeKeyReturn("compute_answer");
object first = WorldBridgeSystem.Instance.InvokeKeyReturnFirst("compute_answer");
```

## One-shot / invoke-once

```csharp
WorldBridgeSystem.Instance.RegisterInvoker("greet_once", args => Debug.Log("Hello once"), DynamicDictionaryInvoker.Layer.Func);
WorldBridgeSystem.Instance.InvokeOnceKey("greet_once"); // invokes, then removes handlers
```

## Conditional / tokened "Pay"

```csharp
// register handler with metadata token
WorldBridgeSystem.Instance.RegisterInvoker("buy_item", args => {
    Debug.Log("Bought item: " + args[0]);
}, DynamicDictionaryInvoker.Layer.Func, id: "shop_1", metadata: "gold");

// only handlers with metadata == token will run
bool executed = WorldBridgeSystem.Instance.PayInvoke("buy_item", token: "gold", "lamp");
```

## Cleaning invoker entries when destroying IDs

```csharp
// Register with id "enemy_42"
var t = WorldBridgeSystem.Instance.RegisterInvoker("enemy_event", args => { /*...*/ }, id: "enemy_42");
// When removing the object:
WorldBridgeSystem.Instance.UnregisterID("enemy_42"); // automatically calls RemoveAllEntriesForId("enemy_42")
//or use a UID system and do GetID() when registering.
```

## Shutdown-aware async cooldown

```csharp
// example inside a MonoBehaviour async method
private async void StartCooldown()
{
    var ct = WorldBridgeSystem.Instance.GetShutdownToken();
    try
    {
        await Task.Delay(TimeSpan.FromSeconds(5), ct);
        // continue only if not cancelled
    }
    catch (OperationCanceledException) { /* shutdown/cleanup */ }
}
```

## DynamicDictionaryInvoker direct usage

```csharp
// register once (auto-removed after first call)
_invoker.RegisterOnce("one_time", args => Debug.Log("fired once"));

// try invoke safely
_invoker.InvokeSafe("maybe_missing", 1, 2);
```

## ExpressionHelpers — boxed and typed getters/setters

```csharp
// boxed getter/setter when you have Type at runtime
var boxedGetter = FracturedStudios.Utils.ExpressionHelpers.CreateBoxedGetter(typeof(MyComponent), "sub.value");
object val = boxedGetter(myComponent);

// boxed setter
var boxedSetter = FracturedStudios.Utils.ExpressionHelpers.CreateBoxedSetter(typeof(MyComponent), "sub.value");
boxedSetter(myComponent, 3.14f);

// typed compiled getter/setter
var typedGetter = FracturedStudios.Utils.ExpressionHelpers.CreateGetter<MyComponent, float>("sub.value");
float v = typedGetter(myComponent);

var typedSetter = FracturedStudios.Utils.ExpressionHelpers.CreateSetter<MyComponent, float>("sub.value");
typedSetter(myComponent, 2.5f);
```

## WorldBridgeRouter usage (route event → method on target ID)

```csharp
// In inspector: add a WorldBridgeRouter and create a route mapping "aim_changed" -> TargetId "aim_responder_01", TargetMethod "OnAimChanged"
// Runtime equivalent:
var router = gameObject.AddComponent<WorldBridgeRouter>();
router.AddRoute("aim_changed", "aim_responder_01", "OnAimChanged", DynamicDictionaryInvoker.Layer.Func);
```

---

## Events & metadata registration

Two common registration styles are shown below. Prefer named methods (not lambdas) when you need to unregister or inspect tokens later.

```csharp
// style A: named method + explicit id + metadata
IDisposable _cellChangedToken = WorldBridgeSystem.Instance?.RegisterInvoker(
    ContainerEventKeys.Scene.CELL_CHANGED,
    OnCellChanged,                              // Named method (not lambda!)
    DynamicDictionaryInvoker.Layer.Func,       // Execution layer
    id: "vision_batch_cell_tracker",          // Debugging ID
    metadata: "vision_batching"               // optional metadata tag
);

// remember to dispose when done (eg. OnDisable)
_cellChangedToken?.Dispose();
```

```csharp
// style B: register many event keys in a loop
var bridge = WorldBridgeSystem.Instance;
string[] playerEventKeys = new[]
{
    EventKeys.Player.SPAWNED,
    EventKeys.Player.LEVEL_UP,
    EventKeys.Player.RESTORED,
    EventKeys.Player.SCENE_CHANGED,
    EventKeys.Player.XP_GAINED,
};

foreach (var k in playerEventKeys)
{
    bridge.RegisterInvoker(k, args => HandlePlayerEvent(k, args), DynamicDictionaryInvoker.Layer.Func, id: "player_events");
}
```

Notes:
- Use `id` to make tokens discoverable and `metadata` for conditional `Pay` calls.
- Prefer storing tokens returned from `RegisterInvoker` if you need to remove specific handlers later (token.Dispose()).

---

## ChapterState examples

Use the `ChapterState` class to gate abilities or alter behavior based on narrative progression.

### Unlocking and checking an ability

```csharp
// create or obtain a chapter state instance (real projects will use ChapterStateService)
var chapter = new ChapterState();
chapter.SetAbilityFlash(true); // unlock flash ability

if (chapter.HasAbilityFlash())
    WorldBridgeSystem.Instance.InvokeKey("player_flash");
else
    Debug.Log("Flash locked");
```

### Named handler that checks ChapterState

```csharp
public class FlashHandler : MonoBehaviour
{
    public ChapterState chapterState; // assign from your ChapterStateService
    private IDisposable _token;

    void OnEnable()
    {
        _token = WorldBridgeSystem.Instance.RegisterInvoker("ability_flash_attempt", OnFlashAttempt, DynamicDictionaryInvoker.Layer.Func, id: "flash_handler", metadata: "abilities");
    }

    void OnDisable()
    {
        _token?.Dispose();
    }

    private void OnFlashAttempt(object[] args)
    {
        if (chapterState == null || !chapterState.HasAbilityFlash())
        {
            Debug.Log("Blocked: flash not unlocked");
            return;
        }

        // perform flash effect
    }
}
```

### Batch-register player events and consult ChapterState in handler

```csharp
var bridge = WorldBridgeSystem.Instance;
string[] playerEventKeys = new[] { EventKeys.Player.SPAWNED, EventKeys.Player.LEVEL_UP };
foreach (var k in playerEventKeys)
    bridge.RegisterInvoker(k, args => HandlePlayerEvent(k, args), DynamicDictionaryInvoker.Layer.Func, id: "player_events", metadata: "player_stream");

void HandlePlayerEvent(string key, object[] args)
{
    // obtain chapter state from your service
    var chapter = new ChapterState();
    if (!chapter.HasAbilityFlash())
    {
        // conditional behavior
    }
}
```


