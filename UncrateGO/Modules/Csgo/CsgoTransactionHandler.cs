﻿using Discord.Commands;
using UncrateGo.Core;
using UncrateGo.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UncrateGo.Modules.Csgo
{
    public static class CsgoTransactionHandler
    {
        //Buy
        public static async Task BuyItemFromMarketAsync(SocketCommandContext context, string itemMarketHash)
        {
            //Get skin data
            var rootWeaponSkins = CsgoDataHandler.GetRootWeaponSkin();

            SkinDataItem selectedMarketSkin = new SkinDataItem();

            //Get market skin cost
            long weaponSkinValue = 0;


            //Make sure skin exists in market
            var marketSkin = rootWeaponSkins.ItemsList.Values.Where(s => s.Name.ToLower().Contains(itemMarketHash.ToLower())).ToList().FirstOrDefault();

            //If searching by direct result cannot be found, search by anything that contains the input
            if (marketSkin == null)
            {
                marketSkin = rootWeaponSkins.ItemsList.Values.Where(s => s.Name.ToLower().Contains(itemMarketHash.ToLower())).ToList().FirstOrDefault();
            }       
            //If it still cannot be found, search by whole words
            if (marketSkin == null)
            {
                marketSkin = FindSimilarItemsByWords(rootWeaponSkins, context, itemMarketHash).FirstOrDefault();
            }

            //Send error if skin does not exist
            if (marketSkin == null)
            {
                await context.Message.Channel.SendMessageAsync(UserInteraction.BoldUserName(context) + $", `{itemMarketHash}` does not exist in the current skin market");
                return;
            }
            else
            {
                //If skin does exist, get info on it
                weaponSkinValue = Convert.ToInt64(rootWeaponSkins.ItemsList.Values.Where(s => s.Name == marketSkin.Name).FirstOrDefault().Price.AllTime.Average);
                selectedMarketSkin.Classid = marketSkin.Classid;
                selectedMarketSkin.Name = marketSkin.Name;

            }

            //Make sure user has enough credits to buy skin
            if (BankingHandler.GetUserCredits(context) < weaponSkinValue)
            {
                await context.Message.Channel.SendMessageAsync($"**{context.Message.Author.ToString().Substring(0, context.Message.Author.ToString().Length - 5)}**, you do not have enough credits to buy `{selectedMarketSkin.Name}` | **{BankingHandler.CreditCurrencyFormatter(weaponSkinValue)}** - **{BankingHandler.CreditCurrencyFormatter(BankingHandler.GetUserCredits(context))}** ");
            }
            else
            {
                //Checks are true, now give user skin and remove credits

                //Remove user credits
                BankingHandler.AddCredits(context, -weaponSkinValue);

                //Add skin to inventory
                CsgoDataHandler.AddItemToUserInventory(context, selectedMarketSkin);

                //Send receipt
                await context.Channel.SendMessageAsync(
                    UserInteraction.BoldUserName(context) + $", you bought `{selectedMarketSkin.Name}`" +
                    $" for **{BankingHandler.CreditCurrencyFormatter(weaponSkinValue)} Credits**");
            }
        }

        //Sell
        public static async Task SellInventoryItemAsync(SocketCommandContext context, string itemMarketHash)
        {
            //Get skin data
            var rootWeaponSkin = CsgoDataHandler.GetRootWeaponSkin();
            var userSkin = CsgoDataHandler.GetUserSkinStorage();

            //Find user selected item, make sure it is owned by user
            var selectedSkinToSell = userSkin.UserSkinEntries
                .Where(s => s.MarketName.ToLower() == itemMarketHash.ToLower())
                .Where(s => s.OwnerID == context.Message.Author.Id)
                .FirstOrDefault();

            //If searching by direct comparison results in nothing, search by contain
            if (selectedSkinToSell == null)
            {
                selectedSkinToSell = userSkin.UserSkinEntries
                .Where(s => s.MarketName.ToLower().Contains(itemMarketHash.ToLower()))
                .Where(s => s.OwnerID == context.Message.Author.Id)
                .FirstOrDefault();
            }

            //Try to search by whole words if still null
            if (selectedSkinToSell == null)
            {
                selectedSkinToSell = FindSimilarItemsByWords(userSkin.UserSkinEntries, context, itemMarketHash).FirstOrDefault();
            }

            if (selectedSkinToSell == null)
            {
                //Send error if user does not have item
                await context.Channel.SendMessageAsync($"**{context.Message.Author.ToString().Substring(0, context.Message.Author.ToString().Length - 5)}**, you do not have `{itemMarketHash}` in your inventory");
            }
            else
            {
                //Get item price
                long weaponSkinValue = Convert.ToInt64(rootWeaponSkin.ItemsList.Values.Where(s => s.Name == selectedSkinToSell.MarketName).FirstOrDefault().Price.AllTime.Average);

                //Give user credits
                BankingHandler.AddCredits(context, weaponSkinValue);


                //Remove items that were selected to be sold
                userSkin.UserSkinEntries.Remove(selectedSkinToSell);

                //Set skin storage
                CsgoDataHandler.SetUserSkinStorage(userSkin);

                //Send receipt
                await context.Channel.SendMessageAsync(
                    UserInteraction.BoldUserName(context) + $", you sold your `{selectedSkinToSell.MarketName}`" +
                    $" for **{BankingHandler.CreditCurrencyFormatter(weaponSkinValue)} Credits**");
            }

        }

        public static async Task SellAllSelectedInventoryItemAsync(SocketCommandContext context, string itemMarketHash)
        {
            //Get skin data
            var rootSkinData = CsgoDataHandler.GetRootWeaponSkin();
            var userSkin = CsgoDataHandler.GetUserSkinStorage();

            //Find ALL user selected items, make sure it is owned by user
            var selectedSkinToSell = userSkin.UserSkinEntries
                .Where(s => s.MarketName.ToLower().Contains(itemMarketHash.ToLower()))
                .Where(s => s.OwnerID == context.Message.Author.Id).ToList();

            //Try to search by whole words if still null
            if (selectedSkinToSell == null || selectedSkinToSell.Count == 0)
            {
                selectedSkinToSell = FindSimilarItemsByWords(userSkin.UserSkinEntries, context, itemMarketHash);
            }

            //Get item prices
            long weaponSkinValue = GetItemValue(selectedSkinToSell, rootSkinData);

            //Give user credits
            BankingHandler.AddCredits(context, weaponSkinValue);

            //Remove skin from inventory
            List<string> filterUserSkinNames = new List<string>();
            foreach (var item in selectedSkinToSell)
            {
                //Remove items that were selected to be sold
                userSkin.UserSkinEntries.Remove(item);

                filterUserSkinNames.Add(item.MarketName);
            }

            if (filterUserSkinNames.Count > 0)
            {
                //Set skin storage
                CsgoDataHandler.SetUserSkinStorage(userSkin);

                //join weapon string
                string soldWeaponsString = string.Join("\n", filterUserSkinNames);
                //Cut string off if length is greater than 1000
                if (soldWeaponsString.Length > 1000) soldWeaponsString = soldWeaponsString.Substring(0, 1000) + "...";

                //Send receipt
                await context.Channel.SendMessageAsync(
                    UserInteraction.BoldUserName(context) + $", you sold your \n`{soldWeaponsString}`" +
                    $" for **{BankingHandler.CreditCurrencyFormatter(weaponSkinValue)} Credits**");
            }
            else
            {
                //Send error if user does not have item
                await context.Channel.SendMessageAsync($"**{context.Message.Author.ToString().Substring(0, context.Message.Author.ToString().Length - 5)}**, you do not have anything containing `{itemMarketHash}` in your inventory");
            }

        }

        public static async Task SellAllInventoryItemAsync(SocketCommandContext context)
        {
            //Get price data
            var rootSkinData = CsgoDataHandler.GetRootWeaponSkin();
            var userSkin = CsgoDataHandler.GetUserSkinStorage();

            //If player has items in inventory, sell!
            if (userSkin.UserSkinEntries.Where(s => s.OwnerID == context.Message.Author.Id).Count() > 0)
            {
                long weaponSkinValue = GetItemValue(userSkin.UserSkinEntries.Where(s => s.OwnerID == context.Message.Author.Id).ToList(), rootSkinData);

                //Give user credits
                BankingHandler.AddCredits(context, weaponSkinValue);

                //Remove user skins from inventory
                var filteredUserSkinEntries = userSkin.UserSkinEntries.Where(s => s.OwnerID != context.Message.Author.Id).ToList();

                //Write to file
                var newUserSkinStorageRoot = new UserSkinStorage
                {
                    UserSkinEntries = filteredUserSkinEntries
                };

                //Set skin storage
                CsgoDataHandler.SetUserSkinStorage(newUserSkinStorageRoot);

                //Send receipt
                await context.Channel.SendMessageAsync(UserInteraction.BoldUserName(context) + $", you sold your inventory for **{BankingHandler.CreditCurrencyFormatter(weaponSkinValue)} Credits**");
            }
            else
            {
                //Send error user does not have any items
                await context.Channel.SendMessageAsync(UserInteraction.BoldUserName(context) + $", your inventory is empty! Go unbox some with `{GuildCommandPrefixManager.GetGuildCommandPrefix(context)}open`");
            }
        }

        private static long GetItemValue(List<UserSkinEntry> userSkins, RootSkinData rootSkinData)
        {
            long weaponSkinValue = 0;
            foreach (var item in userSkins)
            {
                weaponSkinValue += Convert.ToInt64(rootSkinData.ItemsList.Values.Where(s => s.Name == item.MarketName).FirstOrDefault().Price.AllTime.Average);
            }

            return weaponSkinValue;
        }


        //Filtering
        private static List<UserSkinEntry> FindSimilarItemsByWords(List<UserSkinEntry> userSkinEntry, SocketCommandContext context, string inputString)
        {
            List<UserSkinEntry> userSkinEntries = new List<UserSkinEntry>();

            bool match = false;

            string[] tokens = inputString.ToLower().Split(' ');

            //Filter out items not owned by user
            userSkinEntry = userSkinEntry.Where(s => s.OwnerID == context.Message.Author.Id).ToList();

            //Search through userSkinEntry for words that have the specified input string seperated with spaces
            foreach (var item in userSkinEntry)
            {
                for (int i = 0; i < tokens.Length; i++)
                {
                    if (!item.MarketName.ToLower().Contains(tokens[i]))
                    {
                        match = false;
                        break;
                    }

                    match = true;
                    
                }

                if (match) userSkinEntries.Add(item);
            }

            return userSkinEntries;
        }

        private static List<SkinDataItem> FindSimilarItemsByWords(RootSkinData userSkinEntry, SocketCommandContext context, string inputString)
        {
            List<SkinDataItem> userSkinEntries = new List<SkinDataItem>();

            bool match = false;

            string[] tokens = inputString.ToLower().Split(' ');

            //Search through userSkinEntry for words that have the specified input string seperated with spaces
            foreach (var item in userSkinEntry.ItemsList.Values)
            {
                for (int i = 0; i < tokens.Length; i++)
                {
                    if (!item.Name.ToLower().Contains(tokens[i]))
                    {
                        match = false;
                        break;
                    }

                    match = true;

                }

                if (match) userSkinEntries.Add(item);
            }

            return userSkinEntries;
        }
    }
}
