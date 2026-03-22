#!/usr/bin/env python3
"""
Face-centering image preprocessor for TapGallery head sprites.

Detects faces in portrait images using MediaPipe Face Detection,
crops a square region centered on the face, and resizes to a
uniform output size (default 512x512).

Usage:
    python face_crop.py [--input INPUT_DIR] [--output OUTPUT_DIR] [--size 512]

Dependencies:
    pip install opencv-python mediapipe
"""

import argparse
import os
import sys
import urllib.request

import cv2
import mediapipe as mp

MODEL_URL = "https://storage.googleapis.com/mediapipe-models/face_detector/blaze_face_short_range/float16/latest/blaze_face_short_range.tflite"
MODEL_FILENAME = "blaze_face_short_range.tflite"


def ensure_model(script_dir):
    """Download the face detection model if not present. Returns model path."""
    model_path = os.path.join(script_dir, MODEL_FILENAME)
    if not os.path.isfile(model_path):
        print(f"Downloading face detection model to {model_path}...")
        urllib.request.urlretrieve(MODEL_URL, model_path)
        print("Download complete.")
    return model_path


def create_detector(model_path):
    """Create a MediaPipe FaceDetector instance."""
    base_options = mp.tasks.BaseOptions(model_asset_path=model_path)
    options = mp.tasks.vision.FaceDetectorOptions(
        base_options=base_options,
        min_detection_confidence=0.5,
    )
    return mp.tasks.vision.FaceDetector.create_from_options(options)


def detect_faces(image_bgr, detector):
    """Return list of (x, y, w, h) face rectangles using MediaPipe."""
    h_img, w_img = image_bgr.shape[:2]
    image_rgb = cv2.cvtColor(image_bgr, cv2.COLOR_BGR2RGB)
    mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=image_rgb)
    result = detector.detect(mp_image)

    faces = []
    for detection in result.detections:
        bbox = detection.bounding_box
        faces.append((bbox.origin_x, bbox.origin_y, bbox.width, bbox.height))
    return faces


def crop_around_face(image_bgr, face_rect, output_size):
    """Crop a square region centered on the face with padding, then resize."""
    h_img, w_img = image_bgr.shape[:2]
    fx, fy, fw, fh = face_rect

    # Face center
    cx = fx + fw // 2
    cy = fy + fh // 2

    # Square side: face height * 2.2 gives good head + some shoulders framing
    side = int(max(fw, fh) * 2.2)
    # Shift center up slightly so forehead has more room (face detectors
    # tend to crop tight on forehead)
    cy = cy - int(fh * 0.1)

    half = side // 2
    x1 = cx - half
    y1 = cy - half
    x2 = x1 + side
    y2 = y1 + side

    # Clamp to image bounds, keeping square by padding with black
    pad_left = max(0, -x1)
    pad_top = max(0, -y1)
    pad_right = max(0, x2 - w_img)
    pad_bottom = max(0, y2 - h_img)

    x1 = max(0, x1)
    y1 = max(0, y1)
    x2 = min(w_img, x2)
    y2 = min(h_img, y2)

    cropped = image_bgr[y1:y2, x1:x2]

    if pad_left or pad_top or pad_right or pad_bottom:
        cropped = cv2.copyMakeBorder(
            cropped, pad_top, pad_bottom, pad_left, pad_right,
            cv2.BORDER_CONSTANT, value=(0, 0, 0)
        )

    resized = cv2.resize(cropped, (output_size, output_size), interpolation=cv2.INTER_AREA)
    return resized


def process_images(input_dir, output_dir, output_size):
    script_dir = os.path.dirname(os.path.abspath(__file__))
    model_path = ensure_model(script_dir)
    detector = create_detector(model_path)

    os.makedirs(output_dir, exist_ok=True)
    flagged_no_face_dir = os.path.join(output_dir, "flagged_no_face")

    image_extensions = {".jpg", ".jpeg", ".png", ".bmp", ".tiff"}
    files = sorted([
        f for f in os.listdir(input_dir)
        if os.path.splitext(f)[1].lower() in image_extensions
    ])

    if not files:
        print(f"No image files found in {input_dir}")
        return

    processed = []
    skipped_no_face = []
    errors = []

    for filename in files:
        filepath = os.path.join(input_dir, filename)
        image = cv2.imread(filepath)
        if image is None:
            errors.append((filename, "Could not read image"))
            continue

        faces = detect_faces(image, detector)

        if len(faces) == 0:
            os.makedirs(flagged_no_face_dir, exist_ok=True)
            import shutil
            shutil.copy2(filepath, os.path.join(flagged_no_face_dir, filename))
            skipped_no_face.append(filename)
            continue

        # Pick the largest face (by area) if multiple detected
        best_face = max(faces, key=lambda f: f[2] * f[3])

        result = crop_around_face(image, best_face, output_size)
        out_path = os.path.join(output_dir, filename)
        cv2.imwrite(out_path, result)
        processed.append(filename)

    detector.close()

    # Summary report
    print(f"\n{'='*60}")
    print(f"  Face Crop Summary")
    print(f"{'='*60}")
    print(f"  Input:     {input_dir}")
    print(f"  Output:    {output_dir}")
    print(f"  Size:      {output_size}x{output_size}")
    print(f"{'='*60}")
    print(f"  Processed: {len(processed)}")
    print(f"  Skipped:   {len(skipped_no_face)}")
    print(f"  Errors:    {len(errors)}")
    print()

    if skipped_no_face:
        print(f"  SKIPPED (no face detected) -> {flagged_no_face_dir}")
        for name in skipped_no_face:
            print(f"    - {name}")
        print()

    if errors:
        print("  ERRORS:")
        for name, reason in errors:
            print(f"    - {name}: {reason}")
        print()

    print(f"{'='*60}")


def main():
    # Default paths relative to this script's location (Tools/)
    script_dir = os.path.dirname(os.path.abspath(__file__))
    project_root = os.path.dirname(script_dir)
    default_input = os.path.join(project_root, "Assets", "Resources", "TapGallery", "ImgDrop")
    default_output = os.path.join(project_root, "Assets", "Resources", "TapGallery", "ImgDrop_Processed")

    parser = argparse.ArgumentParser(description="Crop and center faces in portrait images")
    parser.add_argument("--input", "-i", default=default_input,
                        help=f"Input directory (default: {default_input})")
    parser.add_argument("--output", "-o", default=default_output,
                        help=f"Output directory (default: {default_output})")
    parser.add_argument("--size", "-s", type=int, default=512,
                        help="Output image size in pixels (default: 512)")
    args = parser.parse_args()

    if not os.path.isdir(args.input):
        print(f"ERROR: Input directory does not exist: {args.input}")
        sys.exit(1)

    process_images(args.input, args.output, args.size)


if __name__ == "__main__":
    main()
