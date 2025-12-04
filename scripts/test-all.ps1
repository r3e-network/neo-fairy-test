param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $Args
)

dotnet test Fairy.Full.sln @Args
