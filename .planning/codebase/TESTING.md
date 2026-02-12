# Testing Patterns

**Analysis Date:** 2026-02-12

## Test Framework

**Runner:**
- xUnit 2.9.3
- Config: `tests/TradingBot.ApiService.Tests/TradingBot.ApiService.Tests.csproj`

**Assertion Library:**
- FluentAssertions 7.0.0

**Mocking Framework:**
- NSubstitute 5.3.0

**Coverage:**
- coverlet.collector 6.0.3 for code coverage collection

**Run Commands:**
```bash
# Run all tests (from solution root)
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true

# Run specific test file
dotnet test tests/TradingBot.ApiService.Tests/TradingBot.ApiService.Tests.csproj

# Watch mode (requires dotnet test watcher tool)
dotnet watch test
```

## Test File Organization

**Location:**
- Tests in separate project: `tests/TradingBot.ApiService.Tests/`
- Parallel to main project: mirrors `TradingBot.ApiService/` structure

**Naming:**
- Pattern: `[Feature].cs` for feature test files
- Class naming: `[FeatureName]Tests` class convention
- Method naming: `[MethodName]_[Scenario]_[ExpectedResult]` pattern

**Structure:**
```
tests/
└── TradingBot.ApiService.Tests/
    ├── Tests.cs                           # Main test file
    ├── TradingBot.ApiService.Tests.csproj
    ├── bin/
    └── obj/
```

## Test Project Configuration

**Project File Settings:**
- Target Framework: `net10.0` (matches main project)
- Implicit Usings: enabled
- Nullable: enabled
- IsTestProject: true
- IsPackable: false

**Global Usings in project file:**
```xml
<ItemGroup>
  <Using Include="Xunit" />
  <Using Include="NSubstitute" />
  <Using Include="FluentAssertions" />
</ItemGroup>
```

This eliminates need for individual using statements in test files.

## Test Structure

**Suite Organization:**
```csharp
namespace TradingBot.ApiService.Tests;

public class Tests
{
    [Fact]
    public void SampleTest()
    {
        Assert.True(true);
    }
}
```

**Pattern observed:**
- Tests marked with `[Fact]` attribute for parameterless tests
- `[Theory]` attribute available for parameterized tests (not yet used)

**Standard Pattern:**
```csharp
public class [FeatureName]Tests
{
    [Fact]
    public async Task [MethodName]_[Scenario]_[ShouldBehavior]()
    {
        // Arrange
        var dependencies = new Mock();

        // Act
        var result = await methodUnderTest();

        // Assert
        result.Should().Be(expectedValue);
    }
}
```

## Mocking

**Framework:** NSubstitute 5.3.0

**Patterns:**
```csharp
// Create substitute
var mockStore = Substitute.For<IOutboxStore>();

// Configure return value
mockStore.GetUnprocessedAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
    .Returns(Task.FromResult(new List<OutboxMessage> { message }));

// Verify calls
await mockStore.Received(1).MarkAsProcessedAsync(message.Id, Arg.Any<CancellationToken>());
```

**What to Mock:**
- External service dependencies: `IOutboxStore`, `IMessageBroker`, `DaprClient`
- Database access layers
- Third-party API clients
- Configuration providers

**What NOT to Mock:**
- Domain models and entities: `OutboxMessage`, `ProcessingStatus`
- Value objects: `Guid`, `DateTimeOffset`
- Simple value types
- Logic under test (unless it depends on mocks)

## Fixtures and Factories

**Test Data:**
Currently minimal test fixtures in codebase. Pattern for adding test data:

```csharp
public class OutboxMessageFixture
{
    public static OutboxMessage CreateValidMessage()
    {
        return new OutboxMessage
        {
            EventName = "TestEvent",
            Payload = "{}",
            ProcessingStatus = ProcessingStatus.Pending
        };
    }
}
```

**Location:**
- Proposed: `tests/TradingBot.ApiService.Tests/Fixtures/` directory
- Class pattern: `[Entity]Fixture.cs`

## Coverage

**Requirements:**
- Not enforced by CI/CD (none detected)
- Target coverage recommended: 70%+ for critical paths

**View Coverage:**
```bash
# Generate coverage report
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# View in Visual Studio or tools like ReportGenerator
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"**/coverage.opencover.xml" -targetdir:"coveragereport"
```

## Test Types

**Unit Tests:**
- Scope: Single class/method in isolation
- Dependencies: Mocked via NSubstitute
- Approach: Arrange-Act-Assert pattern
- Speed: Milliseconds per test

**Example:**
```csharp
[Fact]
public async Task OutboxMessageProcessor_ProcessMessage_MarksAsPublished()
{
    // Arrange
    var mockStore = Substitute.For<IOutboxStore>();
    var mockBroker = Substitute.For<IMessageBroker>();
    var processor = new OutboxMessageProcessor(Logger, mockStore, mockBroker);
    var message = new OutboxMessage { EventName = "Test", Payload = "{}" };

    // Act
    await processor.ProcessOutboxMessagesAsync(message);

    // Assert
    await mockStore.Received(1).MarkAsProcessedAsync(message.Id, Arg.Any<CancellationToken>());
}
```

**Integration Tests:**
- Not yet implemented in test file
- Proposed: Use `Microsoft.EntityFrameworkCore.InMemory` for database testing
- Database: Create real context with in-memory provider
- Pattern: Test multiple layers (service → repository → entity)

**Example (proposed):**
```csharp
[Fact]
public async Task OutboxMessageProcessor_WithRealStore_SavesAndRetrievesMessages()
{
    // Arrange
    var dbContext = new TestDbContext();
    var outboxStore = new EfCoreOutboxStore(dbContext);
    var message = CreateTestMessage();

    // Act
    await outboxStore.AddAsync(message);
    var retrieved = await outboxStore.GetUnprocessedAsync(10);

    // Assert
    retrieved.Should().Contain(m => m.Id == message.Id);
}
```

**E2E Tests:**
- Not implemented
- Proposed approach: WebApplicationFactory for testing endpoints

## Common Patterns

**Async Testing:**
```csharp
[Fact]
public async Task MethodName_Scenario_Expected()
{
    // Arrange
    var mockService = Substitute.For<IService>();
    mockService.GetDataAsync().Returns(Task.FromResult(data));

    // Act
    var result = await methodUnderTest(mockService);

    // Assert
    result.Should().NotBeNull();
}
```

**Error Testing:**
```csharp
[Fact]
public async Task ProcessMessage_ExceedsMaxRetries_MarksAsFailed()
{
    // Arrange
    var message = new OutboxMessage { RetryCount = 3 };
    var mockStore = Substitute.For<IOutboxStore>();

    // Act
    var processor = new OutboxMessageProcessor(Logger, mockStore, Substitute.For<IMessageBroker>());
    await processor.ProcessOutboxMessagesAsync(message);

    // Assert
    await mockStore.Received(1).MarkAsAsync(
        message.Id,
        ProcessingStatus.Failed,
        Arg.Any<CancellationToken>()
    );
}
```

## Test Naming Convention

Follow the pattern: `[MethodName]_[Scenario]_[ExpectedBehavior]`

Examples:
- `OutboxMessageProcessor_ProcessMessage_MarksAsPublished`
- `OutboxMessageBackgroundService_ProcessAsync_ProcessesPendingMessages`
- `OutboxEventPublisher_PublishAsync_AddsMessageToStore`
- `TimeBackgroundService_Execute_RetriesOnException`

## Dependency Injection in Tests

**Pattern:**
- Create test doubles for interface dependencies
- Inject via constructor
- Use NSubstitute for complex behavior

```csharp
public class OutboxMessageProcessorTests
{
    private readonly IOutboxStore _mockStore = Substitute.For<IOutboxStore>();
    private readonly IMessageBroker _mockBroker = Substitute.For<IMessageBroker>();
    private readonly ILogger<OutboxMessageProcessor> _mockLogger = Substitute.For<ILogger<OutboxMessageProcessor>>();

    private OutboxMessageProcessor CreateSut()
    {
        return new OutboxMessageProcessor(_mockLogger, _mockStore, _mockBroker);
    }
}
```

## Best Practices

1. **One assertion focus per test** - Test one behavior per test method
2. **Clear naming** - Test names describe what is being tested and expected
3. **Arrange-Act-Assert** - Clear separation of test phases
4. **Avoid test interdependence** - Each test should be independently runnable
5. **Mock external dependencies** - Keep tests isolated from external services
6. **Use FluentAssertions** - More readable assertions than basic Assert
7. **Don't mock the system under test** - Only mock dependencies
8. **Consistent setup** - Use fixtures/builders for common test data

## Current Test Coverage

**Existing Tests:**
- `tests/TradingBot.ApiService.Tests/Tests.cs` - Single placeholder test

**Critical Areas Needing Tests:**
- `OutboxMessageProcessor` - Core event publishing logic
- `TimeBackgroundService` - Background service execution and error handling
- `OutboxEventPublisher` - Event serialization and storage
- Domain models: `OutboxMessage`, `BaseEntity`
- Service collection extensions for DI registration

---

*Testing analysis: 2026-02-12*
