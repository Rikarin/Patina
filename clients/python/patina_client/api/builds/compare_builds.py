from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx2

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.build_comparison import BuildComparison
from ...models.problem_details import ProblemDetails
from typing import cast



def _get_kwargs(
    id: str,
    *,
    before: str,

) -> dict[str, Any]:
    

    

    params: dict[str, Any] = {}

    params["before"] = before


    params = {k: v for k, v in params.items() if v is not UNSET and v is not None}


    _kwargs: dict[str, Any] = {
        "method": "get",
        "url": "/api/v1/builds/{id}/compare".format(id=quote(str(id), safe=""),),
        "params": params,
    }


    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> BuildComparison | ProblemDetails | None:
    if response.status_code == 200:
        response_200 = BuildComparison.from_dict(response.json())



        return response_200

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> Response[BuildComparison | ProblemDetails]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    before: str,

) -> Response[BuildComparison | ProblemDetails]:
    """ What differs from an earlier build: outputs by key, files by their bytes.

    Args:
        id (str):
        before (str):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[BuildComparison | ProblemDetails]
     """


    kwargs = _get_kwargs(
        id=id,
before=before,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    before: str,

) -> BuildComparison | ProblemDetails | None:
    """ What differs from an earlier build: outputs by key, files by their bytes.

    Args:
        id (str):
        before (str):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        BuildComparison | ProblemDetails
     """


    return sync_detailed(
        id=id,
client=client,
before=before,

    ).parsed

async def asyncio_detailed(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    before: str,

) -> Response[BuildComparison | ProblemDetails]:
    """ What differs from an earlier build: outputs by key, files by their bytes.

    Args:
        id (str):
        before (str):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[BuildComparison | ProblemDetails]
     """


    kwargs = _get_kwargs(
        id=id,
before=before,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    before: str,

) -> BuildComparison | ProblemDetails | None:
    """ What differs from an earlier build: outputs by key, files by their bytes.

    Args:
        id (str):
        before (str):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        BuildComparison | ProblemDetails
     """


    return (await asyncio_detailed(
        id=id,
client=client,
before=before,

    )).parsed
