﻿using ArcadeManager.Actions;
using System.Threading.Tasks;

namespace ArcadeManager.Services;

/// <summary>
/// Interface for the roms service
/// </summary>
public interface IRoms {

    /// <summary>
    /// Copies roms from a folder to another
    /// </summary>
    /// <param name="args">The arguments</param>
    /// <param name="messageHandler">The message handler.</param>
    Task Add(RomsAction args, IMessageHandler messageHandler);

    /// <summary>
    /// Copies roms from a folder to another, from the wizard
    /// </summary>
    /// <param name="args">The arguments</param>
    /// <param name="messageHandler">The message handler.</param>
    Task AddFromWizard(RomsAction args, IMessageHandler messageHandler);

    /// <summary>
    /// Checks a folder for a list of roms
    /// </summary>
    /// <param name="args">The arguments</param>
    /// <param name="messageHandler">The message handler</param>
    /// <returns>The list of missing files</returns>
    Task<string[]> Check(RomsAction args, IMessageHandler messageHandler);

    /// <summary>
    /// Deletes roms from a folder
    /// </summary>
    /// <param name="args">The arguments</param>
    /// <param name="messageHandler">The message handler.</param>
    Task Delete(RomsAction args, IMessageHandler messageHandler);

    /// <summary>
    /// Keeps only listed roms in a folder
    /// </summary>
    /// <param name="args">The arguments</param>
    /// <param name="messageHandler">The message handler.</param>
    Task Keep(RomsAction args, IMessageHandler messageHandler);
}