using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;

using var document = WordprocessingDocument.Open(args[0], false);
var errors = new OpenXmlValidator(FileFormatVersions.Microsoft365).Validate(document).ToArray();
foreach (var error in errors) Console.WriteLine($"{error.Part?.Uri} {error.Path?.XPath}: {error.Description}");
Console.WriteLine($"Errors: {errors.Length}");
return errors.Length == 0 ? 0 : 1;
