### Example 1: Code snippet

```powershell

Import-Module Microsoft.Graph.Security

$params = @{
	displayName = "My Case 1 - Renamed"
	description = "Updated description"
}

Update-MgSecurityCaseEdiscoveryCase -EdiscoveryCaseId $ediscoveryCaseId -BodyParameter $params

```
This example shows how to use the Update-MgSecurityCaseEdiscoveryCase Cmdlet.

