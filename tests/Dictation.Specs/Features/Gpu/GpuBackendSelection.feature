@WHISPER-9
Feature: GPU contact point selects a backend with safe CPU fallback
  As the model layer
  I want one place to decide GPU vs CPU and say why
  So that transcription uses the GPU when it can and never hangs or crashes when it cannot

  Scenario: Vulkan present selects the GPU backend
    Given a usable Vulkan runtime is available
    When the GPU contact point selects a backend
    Then the Vulkan GPU backend is chosen
    And the selection reason cites Vulkan availability

  Scenario: Vulkan absent falls back to CPU
    Given no usable Vulkan runtime is available
    When the GPU contact point selects a backend
    Then the CPU backend is chosen
    And the application continues without hanging or crashing

  Scenario: A failing Vulkan probe falls back to CPU
    Given probing for a Vulkan runtime fails
    When the GPU contact point selects a backend
    Then the CPU backend is chosen
    And the application continues without hanging or crashing
