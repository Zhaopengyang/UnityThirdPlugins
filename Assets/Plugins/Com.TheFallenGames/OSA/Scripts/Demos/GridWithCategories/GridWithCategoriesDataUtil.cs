using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using frame8.Logic.Misc.Other.Extensions;
using Com.TheFallenGames.OSA.DataHelpers;
using Com.TheFallenGames.OSA.CustomAdapters.GridView;
using Com.TheFallenGames.OSA.Demos.Common;

namespace Com.TheFallenGames.OSA.Demos.GridWithCategories
{
    public class GridWithCategoriesDataUtil
	{
		public static List<CategoryModel> CreateRandomCategories(int numCategories, int maxCountInCategory)
		{
			var categories = new List<CategoryModel>(numCategories);
			int currentItemID = 0;
			for (int i = 0; i < numCategories; i++)
			{
				int numItemsInCategory = UnityEngine.Random.Range(1, maxCountInCategory + 2);
				var cat = CreateRandomCategoryModel(currentItemID, numItemsInCategory);
				currentItemID += numItemsInCategory;
				categories.Add(cat);
			}

			return categories;
		}

		static CategoryModel CreateRandomCategoryModel(int itemsStartingID, int numItemsInCategory)
		{
			var cat = new CategoryModel();
			cat.items = new List<CellModel>(numItemsInCategory);
			cat.name = DemosUtil.GetRandomTextBody(5, 100);

			for (int i = 0; i < numItemsInCategory; i++)
			{
				var m = CreateRandomValidItemModel(cat, itemsStartingID + i);
				cat.items.Add(m);
			}

			return cat;
		}

		static CellModel CreateRandomValidItemModel(CategoryModel parentCategory, int id)
		{
			return new CellModel
			{
				parentCategory = parentCategory,
				id = id,
				type = CellModel.CellType.VALID
			};
		}

		static CellModel CreateItemModelForRowCompletion(CategoryModel parentCategory)
		{
			return new CellModel
			{
				parentCategory = parentCategory,
				id = -1,
				type = CellModel.CellType.FOR_ROW_COMPLETION
			};
		}

		static CellModel CreateItemModelInRowSeparatingCategories(CategoryModel parentCategory)
		{
			return new CellModel
			{
				parentCategory = parentCategory,
				id = -1,
				type = CellModel.CellType.IN_ROW_SEPARATING_CATEGORIES
			};
		}

		public static List<CellModel> ConvertCategoriesToListOfItemModels(int itemSlotsPerRow, List<CategoryModel> categories)
		{
			var finalCells = new List<CellModel>();
			for (int i = 0; i < categories.Count; i++)
			{
				var cat = categories[i];

				// Insert an empty row of items to make room for the category's header
				for (int j = 0; j < itemSlotsPerRow; j++)
				{
					var m = CreateItemModelInRowSeparatingCategories(cat);
					finalCells.Add(m);
				}

				// Add the actual cells
				for (int j = 0; j < cat.items.Count; j++)
					finalCells.Add(cat.items[j]);

				// If the category's last row is not full, fill it with empty slots, so they won't be occupied with the ones from the next category
				while (finalCells.Count % itemSlotsPerRow != 0)
					finalCells.Add(CreateItemModelForRowCompletion(cat));
			}

			return finalCells;
		}
	}
}
