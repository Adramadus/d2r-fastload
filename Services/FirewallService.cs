namespace D2RFastLoad.Services;

/// <summary>
/// Windows Firewall management using dynamic COM late-binding.
/// Works with dotnet build (no COMReference / ResolveComReference required).
/// </summary>
public static class FirewallService
{
    private const string RuleName = "D2R FastLoad - Block Battle.net Connections";

    // Protocol / direction / action constants (from NetFwTypeLib)
    private const int FW_IP_PROTOCOL_ANY = 256;
    private const int FW_RULE_DIR_OUT    = 2;
    private const int FW_ACTION_BLOCK    = 0;

    public static bool RuleExists()
    {
        try
        {
            dynamic policy = GetPolicy();
            foreach (dynamic rule in policy.Rules)
                if ((string)rule.Name == RuleName) return true;
            return false;
        }
        catch { return false; }
    }

    public static void AddRule(string d2rExePath)
    {
        dynamic policy = GetPolicy();
        // Remove duplicates first
        try { policy.Rules.Remove(RuleName); } catch { }

        dynamic rule = Activator.CreateInstance(
            Type.GetTypeFromProgID("HNetCfg.FWRule")
            ?? throw new InvalidOperationException("HNetCfg.FWRule not found in registry."))!;

        rule.Name            = RuleName;
        rule.Description     = "Added by Adramadus D2R Fast Load to eliminate Battle.net delays.";
        rule.ApplicationName = d2rExePath;
        rule.Protocol        = FW_IP_PROTOCOL_ANY;
        rule.Direction       = FW_RULE_DIR_OUT;
        rule.Action          = FW_ACTION_BLOCK;
        rule.Enabled         = true;
        policy.Rules.Add(rule);
    }

    public static void RemoveRule()
    {
        try { GetPolicy().Rules.Remove(RuleName); } catch { }
    }

    private static dynamic GetPolicy() =>
        Activator.CreateInstance(
            Type.GetTypeFromProgID("HNetCfg.FwPolicy2")
            ?? throw new InvalidOperationException("HNetCfg.FwPolicy2 not found in registry."))!;
}
