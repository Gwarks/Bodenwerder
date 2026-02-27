open System
open System.Reflection
open System.Reflection.Emit
open System.Globalization

type HelloDelegate = delegate of string*int->int

let testDelegate():unit=
    // Define parameter types
    let helloArgs=[|typeof<string>;typeof<int>|]

    // Create the dynamic method
    let hello=DynamicMethod("Hello",typeof<int>,helloArgs)

    // Get the Console.WriteLine method for a single string parameter
    let writeString=typeof<Console>.GetMethod("WriteLine",[|typeof<string>|])

    // Generate the IL code
    let il=hello.GetILGenerator(256)
    il.Emit(OpCodes.Ldarg_0)
    il.EmitCall(OpCodes.Call,writeString,null)
    il.Emit(OpCodes.Ldarg_1)
    il.Emit(OpCodes.Ret)

    // Define parameters for debugging purposes
    hello.DefineParameter(1,ParameterAttributes.In,"message")|>ignore
    hello.DefineParameter(2,ParameterAttributes.In,"valueToReturn")|>ignore

    // Create a delegate for the dynamic method
    let hi=hello.CreateDelegate(typeof<HelloDelegate>):?>HelloDelegate

    // Execute the dynamic method using the delegate
    printfn "\nUse the delegate to execute the dynamic method:"
    let retval=hi.Invoke("\nHello, World!",42)
    printfn "Invoking delegate hi(\"Hello, World!\", 42) returned: %d" retval
    let retval2=hi.Invoke("\nHi, Mom!",5280)
    printfn "Invoking delegate hi(\"Hi, Mom!\", 5280) returned: %d" retval2

    // Execute the method using Invoke
    printfn "\nUse the Invoke method to execute the dynamic method:"
    let invokeArgs:obj[]=[|"\nHello, World!";42|]
    let objRet=hello.Invoke(null,BindingFlags.ExactBinding,null,invokeArgs,CultureInfo("en-us"))
    printfn "hello.Invoke returned: %O" objRet

    // Display information about the dynamic method
    printfn "\n----- Display information about the dynamic method -----"
    printfn "\nMethod Attributes: %A" hello.Attributes
    printfn "\nCalling convention: %A" hello.CallingConvention
    if isNull hello.DeclaringType then
        printfn "\nDeclaringType is always null for dynamic methods."
    else
        printfn "DeclaringType: %O" hello.DeclaringType
    printfn "\nThis method contains verifiable code. (InitLocals = %b)" hello.InitLocals
    printfn "\nModule: %O" hello.Module
    printfn "\nName: %s" hello.Name

    if isNull hello.ReflectedType then
        printfn "\nReflectedType is null."
    else
        printfn "\nReflectedType: %O" hello.ReflectedType

    if isNull hello.ReturnParameter then
        printfn "\nMethod has no return parameter."
    else
        printfn "\nReturn parameter: %O" hello.ReturnParameter

    printfn "\nReturn type: %O" hello.ReturnType

    let caProvider=hello.ReturnTypeCustomAttributes
    let returnAttributes=caProvider.GetCustomAttributes(true)

    if returnAttributes.Length=0 then
        printfn "\nThe return type has no custom attributes."
    else
        printfn "\nThe return type has the following custom attributes:"
        returnAttributes|>Array.iter(fun attr->printfn "\t%O" attr)

    printfn "\nToString: %s" (hello.ToString())

    let parameters = hello.GetParameters()
    printfn "\nParameters: name, type, ParameterAttributes"

    for p in parameters do
        printfn "\t%s, %O, %A" p.Name p.ParameterType p.Attributes

[<EntryPoint>]
let main(argv)=
    //testDelegate()
    Application.run ()
    0
