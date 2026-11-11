from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx2

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.bake_request import BakeRequest
from ...models.job_state import JobState
from ...models.problem_details import ProblemDetails
from typing import cast



def _get_kwargs(
    *,
    body: BakeRequest,

) -> dict[str, Any]:
    headers: dict[str, Any] = {}


    

    

    _kwargs: dict[str, Any] = {
        "method": "post",
        "url": "/api/v1/jobs/bakes",
    }

    _kwargs["json"] = body.to_dict()

    headers["Content-Type"] = "application/json"

    _kwargs["headers"] = headers
    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> JobState | ProblemDetails | None:
    if response.status_code == 202:
        response_202 = JobState.from_dict(response.json())



        return response_202

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if response.status_code == 422:
        response_422 = ProblemDetails.from_dict(response.json())



        return response_422

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> Response[JobState | ProblemDetails]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    *,
    client: AuthenticatedClient | Client,
    body: BakeRequest,

) -> Response[JobState | ProblemDetails]:
    """ Queues a bake, or answers from the cache when the same bake has been made.

    Args:
        body (BakeRequest): A high-to-low bake. Everything is a path on the Server's machine, and
            the results are files there: a client
            sends paths and gets paths, whichever client it is (plan §8).

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[JobState | ProblemDetails]
     """


    kwargs = _get_kwargs(
        body=body,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    *,
    client: AuthenticatedClient | Client,
    body: BakeRequest,

) -> JobState | ProblemDetails | None:
    """ Queues a bake, or answers from the cache when the same bake has been made.

    Args:
        body (BakeRequest): A high-to-low bake. Everything is a path on the Server's machine, and
            the results are files there: a client
            sends paths and gets paths, whichever client it is (plan §8).

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        JobState | ProblemDetails
     """


    return sync_detailed(
        client=client,
body=body,

    ).parsed

async def asyncio_detailed(
    *,
    client: AuthenticatedClient | Client,
    body: BakeRequest,

) -> Response[JobState | ProblemDetails]:
    """ Queues a bake, or answers from the cache when the same bake has been made.

    Args:
        body (BakeRequest): A high-to-low bake. Everything is a path on the Server's machine, and
            the results are files there: a client
            sends paths and gets paths, whichever client it is (plan §8).

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[JobState | ProblemDetails]
     """


    kwargs = _get_kwargs(
        body=body,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    *,
    client: AuthenticatedClient | Client,
    body: BakeRequest,

) -> JobState | ProblemDetails | None:
    """ Queues a bake, or answers from the cache when the same bake has been made.

    Args:
        body (BakeRequest): A high-to-low bake. Everything is a path on the Server's machine, and
            the results are files there: a client
            sends paths and gets paths, whichever client it is (plan §8).

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        JobState | ProblemDetails
     """


    return (await asyncio_detailed(
        client=client,
body=body,

    )).parsed
