### Example 1: Get the properties a template by ID

```powershell

Import-Module Microsoft.Graph.Identity.SignIns

Get-MgIdentityConditionalAccessTemplate -ConditionalAccessTemplateId $conditionalAccessTemplateId

```
This example will get the properties a template by id

### Example 2: Select details of a template

```powershell

Import-Module Microsoft.Graph.Identity.SignIns

Get-MgIdentityConditionalAccessTemplate -ConditionalAccessTemplateId $conditionalAccessTemplateId -Property "details" 

```
This example will select details of a template

