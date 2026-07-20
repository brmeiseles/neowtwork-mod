# Neowtwork Workshop Workspace

This folder is prepared for Mega Crit's Slay the Spire 2 Workshop uploader.

Generated upload content lives in:

```text
workshop/neowtwork/content/
```

That content is intentionally ignored by git. Regenerate it from the current local build before uploading.

Required files for upload:

```text
workshop/neowtwork/
  workshop.json
  image.png
  content/
    Neowtwork/
      Neowtwork.dll
      Neowtwork.json
      Neowtwork.pck
```

BaseLib Workshop dependency:

```text
3737335127
```

Start with `visibility: "private"`. Only switch to public after the Workshop-loaded version passes smoke testing.
