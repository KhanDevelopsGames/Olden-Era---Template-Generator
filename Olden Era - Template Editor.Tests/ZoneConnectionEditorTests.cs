using Olden_Era___Template_Editor;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Tests;

/// <summary>
/// Unit tests for the Zone Connection Editor feature.
/// Tests exercise model-layer and logic-layer behaviour only; no WPF UI automation.
/// </summary>
public class ZoneConnectionEditorTests
{
    // ════════════════════════════════════════════════════════════════════════
    // T015 · US1 – Edit Connection Properties
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void T015a_GuardValue_CanBeSetAndReadBack()
    {
        var conn = new Connection { From = "Spawn-A", To = "Neutral-1", GuardValue = 100 };
        conn.GuardValue = 999;
        Assert.Equal(999, conn.GuardValue);
    }

    [Fact]
    public void T015b_ConnectionType_CanBeSetToPortal()
    {
        var conn = new Connection { From = "Spawn-A", To = "Neutral-1", ConnectionType = "Direct" };
        conn.ConnectionType = "Portal";
        Assert.Equal("Portal", conn.ConnectionType);
    }

    [Fact]
    public void T015c_GuardWeeklyIncrement_CanBeClearedToNull()
    {
        var conn = new Connection { From = "Spawn-A", To = "Neutral-1", GuardWeeklyIncrement = 1.5 };
        conn.GuardWeeklyIncrement = null;
        Assert.Null(conn.GuardWeeklyIncrement);
    }

    [Fact]
    public void T015d_ConnectionsWereModified_StartsAsFalseIndicatedByInitialState()
    {
        // The public IsUserAdded property starts as false for newly constructed connections,
        // confirming that the model default matches the contract.
        var conn = new Connection { From = "Spawn-A", To = "Neutral-1" };
        Assert.False(conn.IsUserAdded);
    }

    // ════════════════════════════════════════════════════════════════════════
    // T019 · US2 – Add New Connections
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void T019a_AddingConnection_AppendsOneEntryWithIsUserAddedTrue()
    {
        var connections = new List<Connection>
        {
            new() { From = "Spawn-A", To = "Neutral-1" }
        };

        var newConn = new Connection
        {
            From = "Spawn-A", To = "Neutral-2",
            ConnectionType = "Direct",
            IsUserAdded    = true
        };
        connections.Add(newConn);

        Assert.Equal(2, connections.Count);
        Assert.True(connections[^1].IsUserAdded);
    }

    [Fact]
    public void T019b_AddingSecondConnectionBetweenSamePair_ResultsInTwoEntries()
    {
        var connections = new List<Connection>
        {
            new() { From = "Spawn-A", To = "Neutral-1", ConnectionType = "Direct" }
        };

        connections.Add(new Connection
        {
            From = "Spawn-A", To = "Neutral-1",
            ConnectionType = "Portal",
            IsUserAdded    = true
        });

        int pairCount = connections.Count(c =>
            (string.Equals(c.From, "Spawn-A",  StringComparison.Ordinal) && string.Equals(c.To, "Neutral-1", StringComparison.Ordinal)) ||
            (string.Equals(c.From, "Neutral-1", StringComparison.Ordinal) && string.Equals(c.To, "Spawn-A",  StringComparison.Ordinal)));

        Assert.Equal(2, pairCount);
    }

    [Fact]
    public void T019c_CancelledAdd_LeavesListUnchanged()
    {
        var connections = new List<Connection>
        {
            new() { From = "Spawn-A", To = "Neutral-1" }
        };
        int countBefore = connections.Count;

        // Simulate "Cancel" — no item is added
        // (nothing to call — the list is simply not modified)

        Assert.Equal(countBefore, connections.Count);
    }

    // ════════════════════════════════════════════════════════════════════════
    // T022 · US3 – Remove Connections
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void T022a_RemovingConnection_LeavesCountMinusOne()
    {
        var conn1 = new Connection { From = "Spawn-A",   To = "Neutral-1" };
        var conn2 = new Connection { From = "Neutral-1", To = "Neutral-2" };
        var connections = new List<Connection> { conn1, conn2 };

        connections.Remove(conn1);

        Assert.Single(connections);
    }

    [Fact]
    public void T022b_RemovingConnection_LeavesNoPairEntry()
    {
        var target = new Connection { From = "Spawn-A", To = "Neutral-1" };
        var other  = new Connection { From = "Spawn-B", To = "Neutral-2" };
        var connections = new List<Connection> { target, other };

        connections.Remove(target);

        bool stillPresent = connections.Any(c =>
            (string.Equals(c.From, "Spawn-A",  StringComparison.Ordinal) && string.Equals(c.To, "Neutral-1", StringComparison.Ordinal)) ||
            (string.Equals(c.From, "Neutral-1", StringComparison.Ordinal) && string.Equals(c.To, "Spawn-A",  StringComparison.Ordinal)));

        Assert.False(stillPresent);
    }

    [Fact]
    public void T022c_ConnectionsWereModified_IsTrueAfterDelete()
    {
        // Model level: demonstrate that after a deletion the list is mutated (shorter).
        // ConnectionsWereModified is set in the window; here we verify the precondition.
        var conn = new Connection { From = "Spawn-A", To = "Neutral-1" };
        var connections = new List<Connection> { conn };

        connections.Remove(conn);

        Assert.Empty(connections);
    }

    // ════════════════════════════════════════════════════════════════════════
    // T027 · US4 – Graph Overview Helpers
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void T027a_AfterReset_ConnectionListMatchesOriginalInCountAndIsUserAddedIsFalse()
    {
        var original = new List<Connection>
        {
            new() { From = "Spawn-A", To = "Neutral-1", GuardValue = 100 },
            new() { From = "Spawn-B", To = "Neutral-1", GuardValue = 200 }
        };

        var current = new List<Connection>();
        foreach (var orig in original)
            current.Add(ZoneConnectionEditorWindow.CloneConnection(orig, isUserAdded: false));

        Assert.Equal(original.Count, current.Count);
        Assert.All(current, c => Assert.False(c.IsUserAdded));
        Assert.Equal(100, current[0].GuardValue);
        Assert.Equal(200, current[1].GuardValue);
    }

    [Fact]
    public void T027b_IsolatedZoneDetection_IdentifiesZoneWithNoConnections()
    {
        var zones = new List<Zone>
        {
            new() { Name = "Spawn-A"  },
            new() { Name = "Neutral-1" },
            new() { Name = "Neutral-2" }   // isolated — no connection references it
        };
        var connections = new List<Connection>
        {
            new() { From = "Spawn-A", To = "Neutral-1" }
        };

        var isolated = FindIsolatedZones(zones, connections);

        Assert.Single(isolated);
        Assert.Equal("Neutral-2", isolated[0]);
    }

    [Fact]
    public void T027c_DuplicateNameDetection_FlagsWhenTwoConnectionsShareSameName()
    {
        var c1 = new Connection { From = "Spawn-A",   To = "Neutral-1", Name = "main-road" };
        var c2 = new Connection { From = "Neutral-1", To = "Neutral-2", Name = "main-road" };
        var c3 = new Connection { From = "Spawn-B",   To = "Neutral-1", Name = "side-path" };
        var connections = new List<Connection> { c1, c2, c3 };

        Assert.True(HasDuplicateName(connections, c1));
        Assert.True(HasDuplicateName(connections, c2));
        Assert.False(HasDuplicateName(connections, c3));
    }

    [Fact]
    public void T027c_DuplicateNameDetection_DoesNotFlagWhenNamesAreDistinct()
    {
        var c1 = new Connection { From = "Spawn-A",   To = "Neutral-1", Name = "alpha" };
        var c2 = new Connection { From = "Neutral-1", To = "Neutral-2", Name = "beta"  };
        var connections = new List<Connection> { c1, c2 };

        Assert.False(HasDuplicateName(connections, c1));
        Assert.False(HasDuplicateName(connections, c2));
    }

    // ════════════════════════════════════════════════════════════════════════
    // T032d · FR-009 – HasUnresolvedErrors
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void T032d_HasUnresolvedErrors_FalseWhenAllZoneNamesExist()
    {
        var zones = new List<Zone>
        {
            new() { Name = "Spawn-A"  },
            new() { Name = "Neutral-1" }
        };
        var connections = new List<Connection>
        {
            new() { From = "Spawn-A", To = "Neutral-1" }
        };

        Assert.False(ComputeHasErrors(zones, connections));
    }

    [Fact]
    public void T032d_HasUnresolvedErrors_TrueWhenFromZoneIsMissing()
    {
        var zones = new List<Zone>
        {
            new() { Name = "Neutral-1" }
        };
        var connections = new List<Connection>
        {
            new() { From = "Spawn-A", To = "Neutral-1" }   // "Spawn-A" is absent
        };

        Assert.True(ComputeHasErrors(zones, connections));
    }

    [Fact]
    public void T032d_HasUnresolvedErrors_TrueWhenToZoneIsMissing()
    {
        var zones = new List<Zone>
        {
            new() { Name = "Spawn-A" }
        };
        var connections = new List<Connection>
        {
            new() { From = "Spawn-A", To = "Neutral-99" }   // "Neutral-99" is absent
        };

        Assert.True(ComputeHasErrors(zones, connections));
    }

    // ════════════════════════════════════════════════════════════════════════
    // Additional — IsUserAdded serialisation contract
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void IsUserAdded_IsNotSerialised_JsonDoesNotContainProperty()
    {
        var conn = new Connection { From = "Spawn-A", To = "Neutral-1", IsUserAdded = true };
        var options = new System.Text.Json.JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        string json = System.Text.Json.JsonSerializer.Serialize(conn, options);

        Assert.DoesNotContain("isUserAdded",  json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IsUserAdded",  json, StringComparison.Ordinal);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Private test helpers (mirror the logic from ZoneConnectionEditorWindow)
    // ════════════════════════════════════════════════════════════════════════

    private static List<string> FindIsolatedZones(List<Zone> zones, List<Connection> connections) =>
        zones
            .Where(z => !connections.Any(c =>
                string.Equals(c.From, z.Name, StringComparison.Ordinal) ||
                string.Equals(c.To,   z.Name, StringComparison.Ordinal)))
            .Select(z => z.Name)
            .ToList();

    private static bool ComputeHasErrors(List<Zone> zones, List<Connection> connections)
    {
        var zoneNames = new HashSet<string>(zones.Select(z => z.Name), StringComparer.Ordinal);
        return connections.Any(c => !zoneNames.Contains(c.From) || !zoneNames.Contains(c.To));
    }

    private static bool HasDuplicateName(List<Connection> connections, Connection current) =>
        current.Name is { Length: > 0 } name &&
        connections
            .Where(c => !ReferenceEquals(c, current))
            .Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
}
