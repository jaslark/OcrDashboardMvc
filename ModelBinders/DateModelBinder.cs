using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Globalization;

namespace OcrDashboardMvc.ModelBinders
{
    public class DateModelBinder : IModelBinder
    {
        private readonly string[] _dateFormats = { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "yyyy/MM/dd" };

     public Task BindModelAsync(ModelBindingContext bindingContext)
        {
      if (bindingContext == null)
        {
  throw new ArgumentNullException(nameof(bindingContext));
  }

      var modelName = bindingContext.ModelName;
     var valueProviderResult = bindingContext.ValueProvider.GetValue(modelName);

            if (valueProviderResult == ValueProviderResult.None)
            {
      return Task.CompletedTask;
       }

    bindingContext.ModelState.SetModelValue(modelName, valueProviderResult);

         var value = valueProviderResult.FirstValue;

    // Empty string is valid
    if (string.IsNullOrWhiteSpace(value))
            {
  bindingContext.Result = ModelBindingResult.Success(value);
   return Task.CompletedTask;
 }

     // Try to parse the date using multiple formats
     if (DateTime.TryParseExact(value, _dateFormats, CultureInfo.InvariantCulture, 
     DateTimeStyles.None, out DateTime date))
        {
                // Convert to dd/MM/yyyy format for consistency
        var formattedDate = date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
                bindingContext.Result = ModelBindingResult.Success(formattedDate);
       return Task.CompletedTask;
      }

 // Try generic parse as fallback
            if (DateTime.TryParse(value, out date))
        {
        var formattedDate = date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        bindingContext.Result = ModelBindingResult.Success(formattedDate);
                return Task.CompletedTask;
 }

       bindingContext.ModelState.TryAddModelError(
        modelName,
    $"Không th? chuy?n ??i '{value}' thành ngày tháng. Vui lòng s? d?ng ??nh d?ng dd/MM/yyyy");

  return Task.CompletedTask;
        }
    }

    public class DateModelBinderProvider : IModelBinderProvider
    {
        public IModelBinder? GetBinder(ModelBinderProviderContext context)
        {
     if (context == null)
       {
     throw new ArgumentNullException(nameof(context));
       }

            // Apply to string properties named FromDate or ToDate
            if (context.Metadata.ModelType == typeof(string) && 
         (context.Metadata.PropertyName == "FromDate" || context.Metadata.PropertyName == "ToDate"))
   {
                return new DateModelBinder();
      }

            return null;
        }
    }
}
