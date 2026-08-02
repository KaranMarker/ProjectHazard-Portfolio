/*
 * Project Hazard — Selected Portfolio Source
 * Copyright (c) 2026 Karan Marker. All rights reserved.
 *
 * Provided for portfolio review only.
 * The complete Unity project and game assets remain private.
 */
using UnityEngine;

// =========================
// Save Game Manager
// Includes:
// - Temporary local player position storage
// - Singleton access
// =========================
public class SaveGameManager : MonoBehaviour
{
    public static SaveGameManager Instance;

    public Vector3 savePlayerPosition;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SaveSetPlayerPosition(Vector3 newPosition)
    {
        savePlayerPosition = newPosition;
    }

    public Vector3 SaveGetPlayerPosition()
    {
        return savePlayerPosition;
    }
}