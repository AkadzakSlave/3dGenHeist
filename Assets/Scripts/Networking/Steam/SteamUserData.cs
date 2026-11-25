using System;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;

namespace WildBunch.Networking.Steam
{
    /// <summary>
    /// Utility for fetching and caching Steam user avatars and display details.
    /// </summary>
    public static class SteamUserData
    {
        private static readonly Dictionary<ulong, Texture2D> s_AvatarCache = new Dictionary<ulong, Texture2D>();

        /// <summary>
        /// Retrieves the Steam avatar as a Texture2D for the given Steam ID.
        /// </summary>
        public static Texture2D GetAvatar(CSteamID steamId)
        {
            if (!SteamBootstrap.IsSteamAvailable || steamId == CSteamID.Nil)
            {
                return null;
            }

            ulong rawId = steamId.m_SteamID;
            if (s_AvatarCache.TryGetValue(rawId, out Texture2D cached) && cached != null)
            {
                return cached;
            }

            int imageHandle = SteamFriends.GetMediumFriendAvatar(steamId);
            if (imageHandle == -1 || imageHandle == 0)
            {
                // Avatar not yet cached by Steamworks
                return null;
            }

            if (!SteamUtils.GetImageSize(imageHandle, out uint width, out uint height) || width == 0 || height == 0)
            {
                return null;
            }

            byte[] buffer = new byte[width * height * 4];
            if (!SteamUtils.GetImageRGBA(imageHandle, buffer, (int)(width * height * 4)))
            {
                return null;
            }

            // Steam returns RGBA with upside-down rows, flip for Unity Texture2D
            Texture2D texture = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false);
            byte[] flipped = new byte[buffer.Length];
            int rowSize = (int)width * 4;
            for (int y = 0; y < height; y++)
            {
                int srcOffset = y * rowSize;
                int dstOffset = ((int)height - 1 - y) * rowSize;
                Buffer.BlockCopy(buffer, srcOffset, flipped, dstOffset, rowSize);
            }

            texture.LoadRawTextureData(flipped);
            texture.Apply();

            s_AvatarCache[rawId] = texture;
            return texture;
        }

        /// <summary>
        /// Gets friend or current player persona name by Steam ID.
        /// </summary>
        public static string GetPersonaName(CSteamID steamId)
        {
            if (!SteamBootstrap.IsSteamAvailable || steamId == CSteamID.Nil)
            {
                return "Player";
            }

            if (steamId == SteamBootstrap.CurrentSteamId)
            {
                return SteamBootstrap.CurrentPersonaName;
            }

            return SteamFriends.GetFriendPersonaName(steamId);
        }
    }
}
