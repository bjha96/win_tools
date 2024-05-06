#Requires -RunAsAdministrator

#########################################################################################
# DISCLAIMER - Use this script at your own risk. No warranty or support provided by me.
# I have tested this on my home PC running windows 11 (Home) 22H2 Build 22621.1992
# This script requires to be run as Administrator
#########################################################################################

Clear-Host
$DebugPreference = 'Continue'
$ErrorActionPreference = 'Stop'
$VerbosePreference = 'Continue'

# Custom variables, can be changed to suit ones requirement
$WorkGroupName = 'A56-MV2'
$AdmUser = "adm-$Env:Username"

##### NOTICE ###################
#Don't change below this line
################################

function Get-RegkeyValue($keyPath, $keyName) {
    Write-Debug "Checking regKey:$keyName at $keyPath"
    $val = Get-ItemPropertyValue -Path $keyPath -Name $keyName -ErrorAction SilentlyContinue
    return $val
}


function Confirm-User( $name ) {
    $obj = Get-LocalUser -Name $name -ErrorAction SilentlyContinue
    if ($obj) { 
        return 1 
    }
    return 0    
}


function Main {

    #Print information about machine
    & "C:\Windows\System32\systeminfo.exe" | Out-Host

    #Rename built-in Administrator account
    $exists = Confirm-User('Administrator')
    if (1 -eq $exists) {
        Rename-LocalUser -Name 'Administrator' -NewName 'NativeAdmin' -Confirm
    }

    #Rename built-in guest account
    $exists = Confirm-User('Guest')
    if (1 -eq $exists) {
        Rename-LocalUser -Name 'Guest' -NewName 'NativeGuest' -Confirm
    }

    #Change default workgroup name
    #Find the name of Wi-Fi SSID
    #Get-NetAdapter | Where-Object InterfaceType -eq 71
    $exists = Get-WmiObject -Class win32_ComputerSystem | Select-Object -ExpandProperty Workgroup
    if ($exists -ne $WorkGroupName ) {
        Write-Host "Renaming Workgroup $exists to $WorkGroupName"
        Add-Computer -WorkgroupName $WorkGroupName -Confirm
    }
    else {
        Write-Host "Workgroup named $exists already exists."
    }

    #Add a local administrator
    $exists = Confirm-User($AdmUser)
    if (1 -ne $exists) {
        Write-Host "Creating new admin user $AdmUser"
        $Password = Read-Host -Prompt 'Enter a password for the new admin account' -AsSecureString -ErrorAction Stop
        $params = @{
            Name        = $AdmUser
            Password    = $Password
            FullName    = "Administrator $Env:Username"
            Description = 'Alternate administrator account'
        }
        New-LocalUser @params -AccountNeverExpires -PasswordNeverExpires -Confirm

        #Add above created user to local administrators group
        Add-LocalGroupMember -Group 'Administrators' -Member $AdmUser
    }
    else {
        Write-Host "$AdmUser already exists."
    }


    #Enable controlled folder access for default locations
    $exists = Defender\Get-MpPreference | Select-Object -ExpandProperty EnableControlledFolderAccess
    Write-Debug "Defender EnableControlledFolderAccess: $exists"
    if(1 -ne $exists) {
        Write-Host 'Enabling Controlled Folder Access in Defender'
        Defender\Set-MpPreference -EnableControlledFolderAccess Enabled
    }


    #Enable file extension view in explorer
    $keyPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced'
    $exists = Get-RegkeyValue $keyPath 'HideFileExt'
    Write-Debug "Explorer HideFileExt: $exists"
    if($exists) {
        if(0 -ne $exists){
            Write-Host 'Enabling files extension view in Explorer'
            Set-Itemproperty -path $keyPath -Name 'HideFileExt' -value 0
        }
    }
    

    #Set powershell execution policy to RemoteSigned
    $policy=Get-ExecutionPolicy
    Write-Debug "Configured execution policy: $policy"
    if('RemoteSigned' -ne $policy) {
        Write-Host 'Configuring execution policy: RemoteSigned'
        Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope LocalMachine
    }

    #Enable UAC if disabled    
    $uacKeyPath = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System'
    $uac = Get-RegkeyValue $uacKeyPath 'EnableLUA'    
    Write-Debug "UAC status: $uac"
    if('1' -ne "$uac"){
        Write-Host 'Enabling UAC'
        Set-Itemproperty -path $uacKeyPath -Name 'EnableLUA' -value 1
    }

    #List local users
    Write-Host 'Checking users configured...'
    Get-LocalUser | Select-Object Name, Sid, Enabled, Description 2>&1 |Out-Host

    #report installed software
    Write-Host 'Checking installed software on this PC...'
    Get-WmiObject -Class Win32_Product | Sort-object -Property Name | Select-Object Name, Version, InstallLocation
}

#execute main
Main
