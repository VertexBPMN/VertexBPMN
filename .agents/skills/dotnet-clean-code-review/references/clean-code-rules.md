# Readability rules (Rule 8)

## Interpretation for Codex

These are review heuristics, not universal bans. Repository conventions and documented architectural decisions take priority over this style guide. Confirm a concrete readability or maintenance cost before reporting. Examples are illustrative, not instructions to change domain behavior or APIs. Never change a service into an entity, introduce extra classes, or change return behavior solely to imitate an example. Do not use numeric nesting/parameter thresholds as automatic failures.


These are the twelve rules behind Rule 8 in `SKILL.md`. No grep finds them, so check them by reading
the code in scope during the judgement pass.

Report a finding only when the code is genuinely harder to read because of it. One magic number in a
test fixture is not worth a row in the table. A method with five boolean-ish parameters is.

## 1. Naming

Names carry the intent. A reader who has to open three files to learn what a field holds is reading
a naming bug.

- Classes are nouns that describe the object: `ShipmentRepository`, `EmailSender`.
- Methods are verbs or verb phrases: `GetShipmentByNumber`, `SendReceipt`.
- Variables describe the role: `totalPrice`, `isDelivered`.

```csharp
// Before
public class ShipmentService
{
    private DateTime _dateTime;
    private decimal _iNumber;

    public bool HasNoTime() => _dateTime < DateTime.UtcNow;
}

// After
public class Shipment
{
    private DateTime _deliveryDeadline;
    private decimal _declaredValue;

    public bool IsOverdue() => _deliveryDeadline < DateTime.UtcNow;
}
```

## 2. Redundant comments

Comments should explain the **why**, not the **what**. A comment that repeats the line under it goes
stale the first time somebody edits that line.

Flag: comments that restate the code, commented-out code, and historical notes. Git remembers all of
it. Keep comments that explain a decision, and keep XML summaries on public contracts.

```csharp
// Before
// Calculate the total weight of the shipment
var weight = shipment.Items.Sum(x => x.Weight * x.Quantity);

// After
var weight = shipment.GetTotalWeight();
```

## 3. Formatting

Follow the repository `.editorconfig` and existing conventions for indentation, whitespace and braces. Do not assume formatting is enforced by the build. Prefer an existing formatting check over many individual style findings; report whether the check actually ran.

## 4. Nesting

More than two levels of nesting is a review candidate, not an automatic finding. Prefer guard clauses when they simplify the control flow without changing behavior.

```csharp
// Before
if (shipment is not null)
{
    if (shipment.IsPaid)
    {
        if (shipment.Items.Count > 0)
        {
            foreach (var item in shipment.Items) { }
        }
    }
}

// After
if (shipment is null || !shipment.IsPaid || shipment.Items.Count is 0)
{
    return;
}

foreach (var item in shipment.Items) { }
```

## 5. Return early

When a condition is not met, return from the method instead of wrapping the rest of the body in an
`if`. This is the same move as rule 4, seen from the other side.

## 6. The `else` keyword

After an early return, the `else` block is dead weight. Reading an `else` usually means scrolling
back up to find the matching `if`.

```csharp
// Before
if (shipmentAlreadyExists)
{
    logger.LogInformation("Shipment for order '{OrderId}' already exists", request.OrderId);
}
else
{
    await dbContext.AddAsync(shipment, cancellationToken);
    await dbContext.SaveChangesAsync(cancellationToken);
}

// After
if (shipmentAlreadyExists)
{
    logger.LogInformation("Shipment for order '{OrderId}' already exists", request.OrderId);
    return Results.Conflict();
}

await dbContext.AddAsync(shipment, cancellationToken);
await dbContext.SaveChangesAsync(cancellationToken);
```

## 7. Double negatives

Prefer positive boolean names when they make the call site clearer. Established domain terms such as IsDisabled or IsExpired are valid; flag confusing double negation, not every negative name.

Bad: `shipment.IsNotDelivered`, `order.HasNoItems`, `card.IsNotExpired`.
Good: `shipment.IsDelivered`, `order.HasItems`, `card.IsExpired`.

```csharp
// Before
if (!shipment.IsNotDelivered) { }

// After
if (shipment.IsDelivered) { }
```

## 8. Magic numbers and strings

A hard-coded value with no name is a value nobody can safely change. Replace it with a constant or
an enum, so the change happens in one place.

```csharp
// Before
if (shipment.Status == 3) { }
if (customer.Tier == "Gold") { }

// After
if (shipment.Status is ShipmentStatus.Delivered) { }
if (customer.Tier == CustomerTiers.Gold) { }
```

## 9. Parameter count

Three parameters is the comfortable limit. Past that, group the related ones into a request record,
which also lets you add a field later without touching every call site.

```csharp
// Before
public void CreateShipment(string number, string orderId, string address,
    string carrier, string receiverEmail) { }

// After
public void CreateShipment(CreateShipmentRequest request) { }
```

## 10. Single responsibility

A class or a method should have one reason to change. A method whose body reads "fetch, then
transform, then render, then send" is four methods wearing one name.

```csharp
// Before
public class ShipmentReportService
{
    public void GenerateReport()
    {
        // fetch shipments
        // calculate totals
        // render the PDF
        // email it to the customer
    }
}

// After
public class ShipmentReportBuilder { }
public class ShipmentPdfRenderer { }
public class EmailSender { }
```

## 11. Braces

Prefer braces, even on a one-line `if` or `foreach`, unless repository conventions explicitly differ. Without them, the next person to add a line
changes the behaviour without noticing.

```csharp
// Before
if (isValid)
    ProcessShipment();

// After
if (isValid)
{
    ProcessShipment();
}
```

## 12. Never return `null` for a collection

A null collection forces a null check on every caller, and the caller who forgets gets a
`NullReferenceException`. Return an empty collection instead.

```csharp
// Before
public List<ShipmentItem>? GetItems()
{
    return noItemsFound ? null : items;
}

// After
public IReadOnlyList<ShipmentItem> GetItems()
{
    return noItemsFound ? [] : items;
}
```

Use the `[]` collection expression only when the repository language version supports C# 12 or later; otherwise use Array.Empty<T>() or the existing collection convention. Note how this rule and Rule 2 in `SKILL.md`
point at the same signature: return a materialised, non-null, specific type.
