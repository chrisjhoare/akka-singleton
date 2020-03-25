namespace AkkaSingleton.Common.Tests

open AkkaSingleton.Common.Configuration
open System.Collections.Generic
open Microsoft.Extensions.Configuration
open NUnit.Framework
open FsCheck
open System
open FsCheck.NUnit

module ConfigurationTests = 

    type NotNullOrEmptyString() = 
        static member String() :Arbitrary<string> = 
            Arb.Default.NonEmptyString() |> Arb.convert string NonEmptyString
    

    type HandledTypesSubsection = {
        String: string
    }

    type HandledTypesConfig = {
        String: string
        StringList: string list
        OptionalString: string option
        Int: int
        Subsection: HandledTypesSubsection
        OptionalSubSection: HandledTypesSubsection option
        SubsectionList: HandledTypesSubsection list
        
    }
    
    let makeConfig = ConfigBinder.mkConfig<HandledTypesConfig>
    let buildConfig = ConfigValueBuilder.buildValidConfiguration

    [<Property(Arbitrary = [| typeof<NotNullOrEmptyString>|])>]
    let ``Handles types`` (original:HandledTypesConfig) = 
        
        let roundTripped = 
            original 
            |> buildConfig
            |> makeConfig

        original = roundTripped
       
