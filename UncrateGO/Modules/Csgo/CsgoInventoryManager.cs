﻿using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using UncrateGo.Core;
using UncrateGo.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

//Convert.ToInt64(<>.Price.AllTime.Average)

namespace UncrateGo.Modules.Csgo
{
    public static class CsgoInventoryHandler
    {
        private static List<string> embedFieldsMaster = new List<string>();
        private static List<string> embedPriceFieldsMaster = new List<string>();

        public static PaginatedMessage DisplayUserCsgoInventory(SocketCommandContext context)
        {
            string botCommandPrefix = GuildCommandPrefixManager.GetGuildCommandPrefix(context);

            //Reset fields
            embedFieldsMaster = new List<string>();
            embedPriceFieldsMaster = new List<string>();

            //Get user skins from json
            var userSkin = CsgoDataHandler.GetUserSkinStorage();

            List<UserSkinEntry> foundUserSkins = new List<UserSkinEntry>();
            //Filter userSkinEntries xml file down to skins belonging to sender
            foreach (var userSkinEntry in userSkin.UserSkinEntries)
            {
                //Filter skin search to those owned by user
                if (userSkinEntry.OwnerID == context.Message.Author.Id)
                {
                    foundUserSkins.Add(new UserSkinEntry { OwnerID = context.Message.Author.Id, ClassId = userSkinEntry.ClassId, UnboxDate = userSkinEntry.UnboxDate });
                }
            }

            //Generate fields
            AddSkinFieldEntry(foundUserSkins);

            //Configurate paginated message
            var paginationConfig = new PaginationConfig
            {
                AuthorName = context.Message.Author.ToString().Substring(0, context.Message.Author.ToString().Length - 5) + " Inventory",
                AuthorURL = context.Message.Author.GetAvatarUrl(),

                Description = $"Sell items: `{botCommandPrefix}sell [name]` \n Sell all items matching filter: `{botCommandPrefix}sellall [name]`",

                DefaultFieldHeader = "You do not have any items",
                DefaultFieldDescription = $"Go unbox some with `{botCommandPrefix}open` or `{botCommandPrefix}drop`",

                Field1Header = "Name",
                Field2Header = "Market Value",

                Color = new Color(0, 153, 0)
            };

            var paginationManager = new PaginationManager();

            //Generate paginated message
            var pager = paginationManager.GeneratePaginatedMessage(embedFieldsMaster, embedPriceFieldsMaster, paginationConfig);

            return pager;
        }

        private static void AddSkinFieldEntry(List<UserSkinEntry> foundUserSkins)
        {
            var rootWeaponSkin = CsgoDataHandler.GetRootWeaponSkin();

            //For every item belonging to sender
            foreach (var item in foundUserSkins)
            {
                //Find skin entry info
                foreach (var storageSkinEntry in rootWeaponSkin.ItemsList.Values)
                {
                    //Filter by market hash name
                    //LESSON LEARNED: Decode unicode before processing them to avoid them not being recognised!!!!!!!111!!
                    if (UnicodeLiteralConverter.DecodeToNonAsciiCharacters(storageSkinEntry.Classid) == UnicodeLiteralConverter.DecodeToNonAsciiCharacters(item.ClassId))
                    {
                        string skinQualityEmote = GetEmoteBySkinRarity(storageSkinEntry.Rarity, storageSkinEntry.WeaponType);

                        //Add skin entry
                        try
                        {
                            Emote emote = Emote.Parse(skinQualityEmote);

                            //Add skin entry to list
                            embedFieldsMaster.Add(emote + " " + storageSkinEntry.Name);


                            //Filter and Add skin price entry to list
                            embedPriceFieldsMaster.Add(emote + " " + storageSkinEntry.Price.AllTime.Average);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Source);
                        }
                    }
                }
            }

        }

        public static string GetEmoteBySkinRarity(Rarity rarity, WeaponType? weaponType)
        {
            string skinQualityEmote = "<:white:522875796319240193>";

            //Assign quality colors
            if (rarity == Rarity.ConsumerGrade) skinQualityEmote = "<:white:522875796319240193>"; //white
            if (rarity == Rarity.IndustrialGrade) skinQualityEmote = "<:lightblue:522878230848602131>"; //light blue
            if (rarity == Rarity.MilSpecGrade) skinQualityEmote = "<:darkerblue:522878230550544387>"; //darker blue
            if (rarity == Rarity.Restricted) skinQualityEmote = "<:purple:522878233482625034>"; //purple
            if (rarity == Rarity.Classified) skinQualityEmote = "<:pink:522878230856990807>"; //pink
            if (rarity == Rarity.Covert && weaponType != WeaponType.Knife) skinQualityEmote = "<:red:522878230533767199>"; //red
            if (rarity == Rarity.Covert && weaponType == WeaponType.Knife) skinQualityEmote = "<:gold:522878230634692619>"; //gold
            if (rarity == Rarity.Contraband) skinQualityEmote = "<:yellowgold:522878230923968513>"; //rare gold

            return skinQualityEmote;
        }


        public static PaginatedMessage GetCsgoMarketInventory(SocketCommandContext context, string filterString)
        {
            string botCommandPrefix = GuildCommandPrefixManager.GetGuildCommandPrefix(context);

            //Get skin data
            var rootWeaponSkin = CsgoDataHandler.GetRootWeaponSkin();

            List<string> filteredRootWeaponSkin = new List<string>();
            List<string> filteredRootWeaponSkinPrice = new List<string>();

            //Only show if they specified a filter
            if (!string.IsNullOrWhiteSpace(filterString))
            {
                //flter rootWeaponSkin to those with a price found in rootWeaponSkinPrice
                foreach (var skin in rootWeaponSkin.ItemsList.Values)
                {
                    //If filter string is not null, filter market results by user filter string
                    if ((!string.IsNullOrEmpty(filterString) && skin.Name.ToLower().Contains(filterString.ToLower())) || (string.IsNullOrEmpty(filterString)))
                    {
                        string skinQualityEmote = GetEmoteBySkinRarity(skin.Rarity, skin.WeaponType);

                        //Add skin entry

                        Emote emote = Emote.Parse(skinQualityEmote);

                        //Add weapon skin
                        filteredRootWeaponSkin.Add(emote + " " + skin.Name);

                        //Get item value
                        long weaponSkinValue = Convert.ToInt64(skin.Price.AllTime.Average);

                        //Add weapon skin price
                        filteredRootWeaponSkinPrice.Add(emote + " " + weaponSkinValue.ToString());

                    }
                }
            }
            //Configurate paginated message
            var paginationConfig = new PaginationConfig
            {
                AuthorName = "CS:GO Market",
                AuthorURL = "https://i.redd.it/1s0j5e4fhws01.png",

                Description = $"Buy item: `{botCommandPrefix}buy [name]`\nFilter market items by name: `{botCommandPrefix}market [name]`\nView item: `{botCommandPrefix}view [name]`",

                DefaultFieldHeader = "Unable to find specified item!",
                DefaultFieldDescription = $"Broaden your search parameters and try again",

                Field1Header = "Name",
                Field2Header = "Price",

                Color = new Color(0, 204, 0)
            };

            var paginationManager = new PaginationManager();

            //Generate paginated message
            var pager = paginationManager.GeneratePaginatedMessage(filteredRootWeaponSkin, filteredRootWeaponSkinPrice, paginationConfig);

            return pager;
        }
    }
}
