from __future__ import annotations

import os

import pytest

pytestmark = pytest.mark.eval


def test_deepeval_assert_test_smoke() -> None:
    """Optional live DeepEval smoke test.

    This is skipped by default because DeepEval typically requires live judge model
    configuration (for example OpenAI-compatible credentials).
    """
    if os.getenv("DEEPEVAL_RUN_LIVE") != "1":
        pytest.skip("Set DEEPEVAL_RUN_LIVE=1 to run live DeepEval assertions")

    pytest.importorskip("deepeval")
    from deepeval import assert_test
    from deepeval.metrics import HallucinationMetric
    from deepeval.models.llms.openai_model import GPTModel
    from deepeval.test_case import LLMTestCase

    model_name = (
        os.getenv("DEEPEVAL_MODEL") or os.getenv("LLAMA_LLM_MODEL") or "Qwen3-4B-Q4_K_M.gguf"
    )
    base_url = (
        os.getenv("DEEPEVAL_BASE_URL") or os.getenv("LLAMA_LLM_URL") or "http://localhost:8082/v1"
    )
    api_key = os.getenv("DEEPEVAL_API_KEY") or os.getenv("OPENAI_API_KEY") or "local-selfhosted"
    judge_model = GPTModel(
        model=model_name,
        api_key=api_key,
        base_url=base_url,
        temperature=0.0,
    )

    test_case = LLMTestCase(
        input="How do I reset my password?",
        actual_output="Use the reset password link in account settings.",
        expected_output="Use the reset password link in account settings.",
        context=["Use the reset password link in account settings."],
        retrieval_context=["Use the reset password link in account settings."],
    )
    metric = HallucinationMetric(threshold=1.0, model=judge_model)
    assert_test(test_case, [metric])
