﻿using Grand.Business.Core.Interfaces.Catalog.Tax;
using Grand.Business.Core.Interfaces.Checkout.CheckoutAttributes;
using Grand.Business.Core.Extensions;
using Grand.Business.Core.Interfaces.Common.Directory;
using Grand.Business.Core.Interfaces.Common.Localization;
using Grand.Infrastructure;
using Grand.Domain.Catalog;
using Grand.Domain.Common;
using Grand.Domain.Directory;
using Grand.Domain.Orders;
using Grand.Web.Admin.Interfaces;
using Grand.Web.Admin.Models.Orders;
using Microsoft.AspNetCore.Mvc.Rendering;
using Grand.Business.Core.Interfaces.Catalog.Directory;
using Grand.Web.Admin.Extensions.Mapping;

namespace Grand.Web.Admin.Services
{
    public class CheckoutAttributeViewModelService : ICheckoutAttributeViewModelService
    {
        private readonly ICheckoutAttributeService _checkoutAttributeService;
        private readonly ICheckoutAttributeParser _checkoutAttributeParser;
        private readonly ITranslationService _translationService;
        private readonly ITaxCategoryService _taxCategoryService;
        private readonly IWorkContext _workContext;
        private readonly ICurrencyService _currencyService;
        private readonly CurrencySettings _currencySettings;
        private readonly IMeasureService _measureService;
        private readonly MeasureSettings _measureSettings;


        public CheckoutAttributeViewModelService(ICheckoutAttributeService checkoutAttributeService,
            ICheckoutAttributeParser checkoutAttributeParser,
            ITranslationService translationService,
            ITaxCategoryService taxCategoryService,
            IWorkContext workContext,
            ICurrencyService currencyService,
            CurrencySettings currencySettings,
            IMeasureService measureService,
            MeasureSettings measureSettings
            )
        {
            _checkoutAttributeService = checkoutAttributeService;
            _checkoutAttributeParser = checkoutAttributeParser;
            _translationService = translationService;
            _taxCategoryService = taxCategoryService;
            _workContext = workContext;
            _currencyService = currencyService;
            _currencySettings = currencySettings;
            _measureService = measureService;
            _measureSettings = measureSettings;
        }

        #region Utilities

        public virtual async Task PrepareTaxCategories(CheckoutAttributeModel model, CheckoutAttribute checkoutAttribute, bool excludeProperties)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            //tax categories
            var taxCategories = await _taxCategoryService.GetAllTaxCategories();
            model.AvailableTaxCategories.Add(new SelectListItem { Text = _translationService.GetResource("Admin.Configuration.Tax.Settings.TaxCategories.None"), Value = "" });
            foreach (var tc in taxCategories)
                model.AvailableTaxCategories.Add(new SelectListItem { Text = tc.Name, Value = tc.Id, Selected = checkoutAttribute != null && !excludeProperties && tc.Id == checkoutAttribute.TaxCategoryId });
        }

        public virtual async Task PrepareConditionAttributes(CheckoutAttributeModel model, CheckoutAttribute checkoutAttribute)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            //currenty any checkout attribute can have condition.
            model.ConditionAllowed = true;

            if (checkoutAttribute == null)
                return;

            var selectedAttribute = (await _checkoutAttributeParser.ParseCheckoutAttributes(checkoutAttribute.ConditionAttribute)).FirstOrDefault();
            var selectedValues = await _checkoutAttributeParser.ParseCheckoutAttributeValues(checkoutAttribute.ConditionAttribute);

            model.ConditionModel = new ConditionModel {
                EnableCondition = checkoutAttribute.ConditionAttribute.Any(),
                SelectedAttributeId = selectedAttribute != null ? selectedAttribute.Id : "",
                ConditionAttributes = (await _checkoutAttributeService.GetAllCheckoutAttributes(ignoreAcl: false))
                    //ignore this attribute and non-combinable attributes
                    .Where(x => x.Id != checkoutAttribute.Id && x.CanBeUsedAsCondition())
                    .Select(x =>
                        new AttributeConditionModel {
                            Id = x.Id,
                            Name = x.Name,
                            AttributeControlType = x.AttributeControlTypeId,
                            Values = x.CheckoutAttributeValues
                                .Select(v => new SelectListItem {
                                    Text = v.Name,
                                    Value = v.Id.ToString(),
                                    Selected = selectedAttribute != null && selectedAttribute.Id == x.Id && selectedValues.Any(sv => sv.Id == v.Id)
                                }).ToList()
                        }).ToList()
            };
        }

        protected virtual async Task SaveConditionAttributes(CheckoutAttribute checkoutAttribute, CheckoutAttributeModel model)
        {
            var conditionAttributes = new List<CustomAttribute>();
            if (model.ConditionModel.EnableCondition)
            {
                var attribute = await _checkoutAttributeService.GetCheckoutAttributeById(model.ConditionModel.SelectedAttributeId);
                if (attribute != null)
                {
                    switch (attribute.AttributeControlTypeId)
                    {
                        case AttributeControlType.DropdownList:
                        case AttributeControlType.RadioList:
                        case AttributeControlType.ColorSquares:
                        case AttributeControlType.ImageSquares:
                            {
                                var selectedAttribute = model.ConditionModel.ConditionAttributes
                                    .FirstOrDefault(x => x.Id == model.ConditionModel.SelectedAttributeId);
                                var selectedValue = selectedAttribute?.SelectedValueId;
                                if (!string.IsNullOrEmpty(selectedValue))
                                    conditionAttributes = _checkoutAttributeParser.AddCheckoutAttribute(conditionAttributes, attribute, selectedValue).ToList();
                                else
                                    conditionAttributes = _checkoutAttributeParser.AddCheckoutAttribute(conditionAttributes, attribute, string.Empty).ToList();
                            }
                            break;
                        case AttributeControlType.Checkboxes:
                            {
                                var selectedAttribute = model.ConditionModel.ConditionAttributes
                                    .FirstOrDefault(x => x.Id == model.ConditionModel.SelectedAttributeId);
                                var selectedValues = selectedAttribute?.Values.Where(x => x.Selected).Select(x => x.Value);
                                if (selectedValues.Any())
                                    foreach (var value in selectedValues)
                                        conditionAttributes = _checkoutAttributeParser.AddCheckoutAttribute(conditionAttributes, attribute, value).ToList();
                                else
                                    conditionAttributes = _checkoutAttributeParser.AddCheckoutAttribute(conditionAttributes, attribute, string.Empty).ToList();
                            }
                            break;
                        case AttributeControlType.ReadonlyCheckboxes:
                        case AttributeControlType.TextBox:
                        case AttributeControlType.MultilineTextbox:
                        case AttributeControlType.Datepicker:
                        case AttributeControlType.FileUpload:
                        default:
                            //these attribute types are not supported as conditions
                            break;
                    }
                }
            }
            checkoutAttribute.ConditionAttribute = conditionAttributes;
        }


        #endregion

        public virtual async Task<IEnumerable<CheckoutAttributeModel>> PrepareCheckoutAttributeListModel()
        {
            var checkoutAttributes = await _checkoutAttributeService.GetAllCheckoutAttributes(ignoreAcl: true);
            return checkoutAttributes.Select((Func<CheckoutAttribute, CheckoutAttributeModel>)(x =>
                {
                    var attributeModel = x.ToModel();
                    attributeModel.AttributeControlTypeName = x.AttributeControlTypeId.GetTranslationEnum(_translationService, _workContext);
                    return attributeModel;
                }));
        }
        public virtual async Task<IEnumerable<CheckoutAttributeValueModel>> PrepareCheckoutAttributeValuesModel(string checkoutAttributeId)
        {
            var checkoutAttribute = await _checkoutAttributeService.GetCheckoutAttributeById(checkoutAttributeId);
            var values = checkoutAttribute.CheckoutAttributeValues;
            return values.Select(x => new CheckoutAttributeValueModel
            {
                Id = x.Id,
                CheckoutAttributeId = x.CheckoutAttributeId,
                Name = checkoutAttribute.AttributeControlTypeId != AttributeControlType.ColorSquares ? x.Name : $"{x.Name} - {x.ColorSquaresRgb}",
                ColorSquaresRgb = x.ColorSquaresRgb,
                PriceAdjustment = x.PriceAdjustment,
                WeightAdjustment = x.WeightAdjustment,
                IsPreSelected = x.IsPreSelected,
                DisplayOrder = x.DisplayOrder
            });
        }
        public virtual async Task<CheckoutAttributeModel> PrepareCheckoutAttributeModel()
        {
            var model = new CheckoutAttributeModel();
            //tax categories
            await PrepareTaxCategories(model, null, true);
            //condition
            await PrepareConditionAttributes(model, null);
            return model;
        }

        public virtual async Task<CheckoutAttributeValueModel> PrepareCheckoutAttributeValueModel(string checkoutAttributeId)
        {
            var checkoutAttribute = await _checkoutAttributeService.GetCheckoutAttributeById(checkoutAttributeId);
            var model = new CheckoutAttributeValueModel {
                CheckoutAttributeId = checkoutAttributeId,
                PrimaryStoreCurrencyCode = (await _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId)).CurrencyCode,
                BaseWeightIn = (await _measureService.GetMeasureWeightById(_measureSettings.BaseWeightId)).Name,
                //color squares
                DisplayColorSquaresRgb = checkoutAttribute.AttributeControlTypeId == AttributeControlType.ColorSquares,
                ColorSquaresRgb = "#000000"
            };

            return model;
        }
        public virtual async Task<CheckoutAttributeValueModel> PrepareCheckoutAttributeValueModel(CheckoutAttribute checkoutAttribute, CheckoutAttributeValue checkoutAttributeValue)
        {
            var model = checkoutAttributeValue.ToModel();
            model.DisplayColorSquaresRgb = checkoutAttribute.AttributeControlTypeId == AttributeControlType.ColorSquares;
            model.PrimaryStoreCurrencyCode = (await _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId)).CurrencyCode;
            model.BaseWeightIn = (await _measureService.GetMeasureWeightById(_measureSettings.BaseWeightId)).Name;

            return model;
        }
        public virtual async Task<CheckoutAttribute> InsertCheckoutAttributeModel(CheckoutAttributeModel model)
        {
            var checkoutAttribute = model.ToEntity();
            await _checkoutAttributeService.InsertCheckoutAttribute(checkoutAttribute);

            return checkoutAttribute;
        }
        public virtual async Task<CheckoutAttribute> UpdateCheckoutAttributeModel(CheckoutAttribute checkoutAttribute, CheckoutAttributeModel model)
        {
            checkoutAttribute = model.ToEntity(checkoutAttribute);
            await SaveConditionAttributes(checkoutAttribute, model);
            await _checkoutAttributeService.UpdateCheckoutAttribute(checkoutAttribute);
            return checkoutAttribute;
        }

        public virtual async Task<CheckoutAttributeValue> InsertCheckoutAttributeValueModel(CheckoutAttribute checkoutAttribute, CheckoutAttributeValueModel model)
        {
            var cav = model.ToEntity();
            checkoutAttribute.CheckoutAttributeValues.Add(cav);
            await _checkoutAttributeService.UpdateCheckoutAttribute(checkoutAttribute);
            return cav;
        }

        public virtual async Task<CheckoutAttributeValue> UpdateCheckoutAttributeValueModel(CheckoutAttribute checkoutAttribute, CheckoutAttributeValue checkoutAttributeValue, CheckoutAttributeValueModel model)
        {
            checkoutAttributeValue = model.ToEntity(checkoutAttributeValue);
            await _checkoutAttributeService.UpdateCheckoutAttribute(checkoutAttribute);
            return checkoutAttributeValue;
        }
    }
}
