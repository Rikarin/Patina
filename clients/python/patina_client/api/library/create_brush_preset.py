from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.brush_preset_create_request import BrushPresetCreateRequest
from ...models.brush_preset_info import BrushPresetInfo
from ...models.problem_details import ProblemDetails
from typing import cast



def _get_kwargs(
    *,
    body: BrushPresetCreateRequest,

) -> dict[str, Any]:
    headers: dict[str, Any] = {}


    

    

    _kwargs: dict[str, Any] = {
        "method": "post",
        "url": "/api/v1/library/brushes",
    }

    _kwargs["json"] = body.to_dict()

    headers["Content-Type"] = "application/json"

    _kwargs["headers"] = headers
    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> BrushPresetInfo | ProblemDetails | None:
    if response.status_code == 200:
        response_200 = BrushPresetInfo.from_dict(response.json())



        return response_200

    if response.status_code == 400:
        response_400 = ProblemDetails.from_dict(response.json())



        return response_400

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if response.status_code == 409:
        response_409 = ProblemDetails.from_dict(response.json())



        return response_409

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> Response[BrushPresetInfo | ProblemDetails]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    *,
    client: AuthenticatedClient | Client,
    body: BrushPresetCreateRequest,

) -> Response[BrushPresetInfo | ProblemDetails]:
    """ Saves a brush as a new preset, a document of the project.

    Args:
        body (BrushPresetCreateRequest): The body of `POST /api/v1/library/brushes`, which saves a
            new preset in a project.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[BrushPresetInfo | ProblemDetails]
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
    body: BrushPresetCreateRequest,

) -> BrushPresetInfo | ProblemDetails | None:
    """ Saves a brush as a new preset, a document of the project.

    Args:
        body (BrushPresetCreateRequest): The body of `POST /api/v1/library/brushes`, which saves a
            new preset in a project.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        BrushPresetInfo | ProblemDetails
     """


    return sync_detailed(
        client=client,
body=body,

    ).parsed

async def asyncio_detailed(
    *,
    client: AuthenticatedClient | Client,
    body: BrushPresetCreateRequest,

) -> Response[BrushPresetInfo | ProblemDetails]:
    """ Saves a brush as a new preset, a document of the project.

    Args:
        body (BrushPresetCreateRequest): The body of `POST /api/v1/library/brushes`, which saves a
            new preset in a project.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[BrushPresetInfo | ProblemDetails]
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
    body: BrushPresetCreateRequest,

) -> BrushPresetInfo | ProblemDetails | None:
    """ Saves a brush as a new preset, a document of the project.

    Args:
        body (BrushPresetCreateRequest): The body of `POST /api/v1/library/brushes`, which saves a
            new preset in a project.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        BrushPresetInfo | ProblemDetails
     """


    return (await asyncio_detailed(
        client=client,
body=body,

    )).parsed
