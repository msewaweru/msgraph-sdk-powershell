### Example 1: Code snippet

```powershell

Import-Module Microsoft.Graph.Beta.ChangeNotifications

$params = @{
	expirationDateTime = [System.DateTime]::Parse("2016-11-22T18:23:45.9356913Z")
}

Update-MgBetaSubscription -SubscriptionId $subscriptionId -BodyParameter $params

```
This example shows how to use the Update-MgBetaSubscription Cmdlet.

